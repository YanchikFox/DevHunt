"use client"

import { useEffect, useState, useCallback } from "react"

const STORAGE_KEY = "devhunt-sidebar-collapsed"

export function useSidebar() {
  const [collapsed, setCollapsed] = useState(false)

  useEffect(() => {
    const saved = localStorage.getItem(STORAGE_KEY)
    if (saved === "true") {
      setCollapsed(true)
    }
  }, [])

  const toggle = useCallback(() => {
    setCollapsed((prev) => {
      const next = !prev
      localStorage.setItem(STORAGE_KEY, String(next))
      return next
    })
  }, [])

  return { collapsed, toggle }
}
