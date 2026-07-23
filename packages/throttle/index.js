import os from 'os'

/**
 * Factory that produces a throttling/backpressure middleware with shared metrics.
 * @param {object} options
 * @param {import('../..//integration-gateway/src/utils/logger.js').logger|console} options.logger
 * @param {number} [options.maxConcurrent=100]
 * @param {number} [options.maxQueueSize=200]
 * @param {number} [options.cpuThreshold=0.8]
 * @param {number} [options.memoryThreshold=0.9]
 * @param {number} [options.metricsRefreshMs=2000]
 */
export function createThrottle ({
  logger,
  maxConcurrent = 100,
  maxQueueSize = 200,
  cpuThreshold = 0.8,
  memoryThreshold = 0.9,
  metricsRefreshMs = 2000
} = {}) {
  if (!logger) {
    throw new Error('Throttle factory requires a logger instance')
  }

  let activeRequests = 0
  const requestQueue = []
  let isProcessingQueue = false
  let cachedCpuLoad = 0
  let cachedMemoryUsage = 0

  function getCpuLoad () {
    const cpus = os.cpus()
    let totalIdle = 0
    let totalTick = 0

    cpus.forEach((cpu) => {
      for (const type in cpu.times) {
        if (!Object.prototype.hasOwnProperty.call(cpu.times, type)) {
          continue
        }
        totalTick += cpu.times[type]
      }
      totalIdle += cpu.times.idle
    })

    const idle = totalIdle / cpus.length
    const total = totalTick / cpus.length
    const usage = 1 - idle / total
    return usage
  }

  function getMemoryUsage () {
    const totalMemory = os.totalmem()
    const freeMemory = os.freemem()
    const usedMemory = totalMemory - freeMemory
    return usedMemory / totalMemory
  }

  function sampleSystemMetrics () {
    cachedCpuLoad = getCpuLoad()
    cachedMemoryUsage = getMemoryUsage()
  }

  sampleSystemMetrics()
  const metricsTimer = setInterval(sampleSystemMetrics, metricsRefreshMs)
  if (metricsTimer.unref) {
    metricsTimer.unref()
  }

  function isSystemOverloaded () {
    const overloaded =
      cachedCpuLoad > cpuThreshold || cachedMemoryUsage > memoryThreshold

    if (overloaded) {
      logger.warn(
        `System overloaded: CPU=${(cachedCpuLoad * 100).toFixed(1)}%, Memory=${(
          cachedMemoryUsage * 100
        ).toFixed(1)}%`
      )
    }

    return overloaded
  }

  const createThrottledHandlers = (res, finish) => {
    const originalEnd = res.end.bind(res)
    const originalJson = res.json.bind(res)

    const throttledEnd = (...args) => {
      finish()
      return originalEnd(...args)
    }

    const throttledJson = (...args) => {
      finish()
      return originalJson(...args)
    }

    return { throttledEnd, throttledJson }
  }

  async function processQueue () {
    if (isProcessingQueue) {
      return
    }

    isProcessingQueue = true

    const createFinish = (resolve) => {
      let finished = false
      const finish = () => {
        if (!finished) {
          finished = true
          activeRequests -= 1
          resolve()
        }
      }

      return { finish, isFinished: () => finished }
    }

    while (requestQueue.length > 0 && activeRequests < maxConcurrent) {
      if (isSystemOverloaded()) {
        await new Promise((resolve) => setTimeout(resolve, 100))
        continue
      }

      const { req, res, next } = requestQueue.shift()
      activeRequests += 1

      try {
        await new Promise((resolve, reject) => {
          const { finish, isFinished } = createFinish(resolve)
          const { throttledEnd, throttledJson } = createThrottledHandlers(
            res,
            finish
          )

          res.end = throttledEnd
          res.json = throttledJson

          try {
            next()
          } catch (error) {
            reject(error)
          }

          setTimeout(() => {
            if (!isFinished()) {
              logger.warn(`Request timeout for ${req.path}`)
              finish()
            }
          }, 30_000)
        })
      } catch (error) {
        activeRequests -= 1
        logger.error('Error processing queued request:', error)
      }
    }

    isProcessingQueue = false
  }

  function throttle (req, res, next) {
    if (isSystemOverloaded()) {
      return res.status(503).json({
        error: 'Service temporarily unavailable',
        reason: 'System overloaded',
        retryAfter: 5
      })
    }

    if (activeRequests < maxConcurrent) {
      activeRequests += 1

      const originalEnd = res.end.bind(res)
      const originalJson = res.json.bind(res)
      let finished = false

      const finish = () => {
        if (!finished) {
          finished = true
          activeRequests -= 1
          processQueue().catch((error) =>
            logger.error('Error draining queue:', error)
          )
        }
      }

      res.end = function throttledEnd (...args) {
        finish()
        return originalEnd(...args)
      }

      res.json = function throttledJson (...args) {
        finish()
        return originalJson(...args)
      }

      return next()
    }

    if (requestQueue.length >= maxQueueSize) {
      logger.warn(
        `Request queue full (${requestQueue.length}), rejecting request`
      )
      return res.status(503).json({
        error: 'Service temporarily unavailable',
        reason: 'Request queue full',
        retryAfter: 10,
        queueLength: requestQueue.length
      })
    }

    requestQueue.push({ req, res, next })
    logger.debug(
      `Request queued: ${req.path} (queue: ${requestQueue.length}, active: ${activeRequests})`
    )

    return processQueue().catch((error) =>
      logger.error('Error draining queued requests:', error)
    )
  }

  function getMetrics () {
    return {
      activeRequests,
      queueLength: requestQueue.length,
      maxConcurrentRequests: maxConcurrent,
      maxQueueSize,
      cpuLoad: cachedCpuLoad,
      memoryUsage: cachedMemoryUsage,
      isOverloaded: isSystemOverloaded()
    }
  }

  function shutdown () {
    clearInterval(metricsTimer)
  }

  return {
    throttle,
    getMetrics,
    shutdown
  }
}
