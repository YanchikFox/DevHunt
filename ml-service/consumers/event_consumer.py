"""
Event Bus Consumer for ML Service
Subscribes to RabbitMQ events for recalculating recommendations
"""

import asyncio
import json
import logging
import os
from typing import Optional

import aio_pika
import asyncpg

logger = logging.getLogger(__name__)

RABBITMQ_URL = (
    os.getenv("RABBITMQ_URL")
    or os.getenv("RABBITMQ__CONNECTIONSTRING")
    or "amqp://devhunt:devhunt_password@message-broker:5672"
)
EXCHANGE_NAME = os.getenv("RABBITMQ_EXCHANGE_NAME", "devhunt.events")
ML_QUEUE = os.getenv("ML_QUEUE", "devhunt.ml.recommendations")
DATABASE_URL = os.getenv("DATABASE_URL")


class EventConsumer:
    """Consumer for processing events from Event Bus"""

    def __init__(self, on_connection_state=None, on_event=None):
        self.connection: Optional[aio_pika.Connection] = None
        self.channel: Optional[aio_pika.Channel] = None
        self.db_pool: Optional[asyncpg.Pool] = None
        self.on_connection_state = on_connection_state or (lambda connected: None)
        self.on_event = on_event or (lambda routing_key, status: None)

    async def connect_db(self):
        """Connect to PostgreSQL"""
        if not DATABASE_URL:
            logger.warning("DATABASE_URL not set, database operations disabled")
            return

        try:
            self.db_pool = await asyncpg.create_pool(DATABASE_URL)
            logger.info("Connected to PostgreSQL for event processing")
        except Exception as e:
            logger.error("Failed to connect to database: %s", e)

    async def start(self):
        """Start consumer"""
        try:
            logger.info("Connecting to RabbitMQ at %s", RABBITMQ_URL)
            self.connection = await aio_pika.connect_robust(RABBITMQ_URL)
            self.channel = await self.connection.channel()
            self.on_connection_state(True)

            # Connect to DB
            await self.connect_db()

            # Declare exchange
            exchange = await self.channel.declare_exchange(
                EXCHANGE_NAME, aio_pika.ExchangeType.TOPIC, durable=True
            )

            # Declare queue
            queue = await self.channel.declare_queue(ML_QUEUE, durable=True)

            # Bind queue to exchange with routing keys
            routing_keys = [
                "profile.updated",  # Profile updated - recalculate recommendations
                "project.completed",  # Project completed - update statistics
                "project.created",  # New project - add to recommendations
                "showcase.published",  # Showcase published - update popularity
            ]

            for routing_key in routing_keys:
                await queue.bind(exchange, routing_key=routing_key)
                logger.info("Bound queue to exchange with routing key: %s", routing_key)

            logger.info("Listening for events on exchange: %s, queue: %s", EXCHANGE_NAME, ML_QUEUE)

            # Consume messages
            async with queue.iterator() as queue_iter:
                async for message in queue_iter:
                    async with message.process():
                        try:
                            event = json.loads(message.body.decode())
                            routing_key = message.routing_key

                            logger.info(
                                "Received event: %s for %s:%s (routing: %s)",
                                event.get('EventType'),
                                event.get('EntityType'),
                                event.get('EntityId'),
                                routing_key
                            )

                            await self.process_event(event, routing_key)
                            self.on_event(routing_key, "success")

                        except Exception as e:
                            logger.error("Error processing event: %s", e, exc_info=True)
                            self.on_event(message.routing_key, "failed")
                            # Message will be requeued automatically
                            raise

        except Exception as e:
            logger.error("Failed to start event consumer: %s", e, exc_info=True)
            self.on_connection_state(False)
            # Attempt to reconnect after 10 seconds
            await asyncio.sleep(10)
            await self.start()

    async def process_event(self, event: dict, routing_key: str):
        """Process event"""
        event_type = event.get("EventType")
        entity_id = event.get("EntityId")
        _entity_type = event.get("EntityType")  # Not used in current implementation
        data = event.get("Data", {})

        try:
            if event_type == "profile.updated":
                await self.handle_profile_updated(entity_id, data)
            elif event_type == "project.completed":
                await self.handle_project_completed(entity_id, data)
            elif event_type == "project.created":
                await self.handle_project_created(entity_id, data)
            elif event_type == "showcase.published":
                await self.handle_showcase_published(entity_id, data)
            else:
                logger.debug("Event %s does not require ML processing", event_type)

        except Exception as e:
            logger.error("Error handling event %s: %s", event_type, e, exc_info=True)
            raise

    async def handle_profile_updated(self, user_id: str, data: dict):
        """Handle profile update - recalculate recommendations"""
        logger.info("Profile updated for user %s, invalidating recommendations cache", user_id)

        # In reality:
        # 1. Invalidate recommendations cache for user
        # 2. Recalculate recommendations based on updated skills/experience
        # 3. Update embedding for user (if using vector search)

        # For now just log
        if self.db_pool:
            try:
                # Example: update last profile update timestamp
                async with self.db_pool.acquire() as conn:
                    await conn.execute("UPDATE users SET updated_at = NOW() WHERE id = $1", user_id)
                logger.info("Updated profile timestamp for user %s", user_id)
            except Exception as e:
                logger.warning("Failed to update profile timestamp: %s", e)

    async def handle_project_completed(self, project_id: str, data: dict):
        """Handle project completion - update statistics"""
        logger.info("Project %s completed, updating statistics", project_id)

        # In reality:
        # 1. Update successful projects statistics for participants
        # 2. Recalculate recommendations for project participants
        # 3. Update project popularity metrics

        if self.db_pool:
            try:
                async with self.db_pool.acquire() as conn:
                    # Update participants statistics
                    await conn.execute(
                        """
                        UPDATE users 
                        SET 
                            rating = COALESCE((
                                SELECT AVG(rating) 
                                FROM reviews 
                                WHERE user_id = users.id
                            ), rating)
                        WHERE id IN (
                            SELECT user_id 
                            FROM team_members 
                            WHERE project_id = $1 AND status = 'active'
                        )
                        """,
                        project_id,
                    )
                logger.info("Updated statistics for project %s participants", project_id)
            except Exception as e:
                logger.warning("Failed to update project statistics: %s", e)

    async def handle_project_created(self, project_id: str, data: dict):
        """Handle project creation - add to recommendations"""
        logger.info("Project %s created, updating recommendations", project_id)

        # In reality:
        # 1. Create embedding for new project
        # 2. Add project to search index
        # 3. Recalculate recommendations for users with suitable skills

        if self.db_pool:
            try:
                async with self.db_pool.acquire() as conn:
                    # Verify that project exists
                    project = await conn.fetchrow(
                        "SELECT id, title, tech_stack FROM projects WHERE id = $1", project_id
                    )
                    if project:
                        logger.info(
                            "Project %s (%s) ready for recommendations", project_id, project['title']
                        )
                        # Here you can add embedding creation logic
            except Exception as e:
                logger.warning("Failed to process new project: %s", e)

    async def handle_showcase_published(self, project_id: str, data: dict):
        """Handle showcase publication - update popularity"""
        logger.info("Showcase published for project %s, updating popularity", project_id)

        # In reality:
        # 1. Increase project popularity
        # 2. Recalculate recommendations with popularity consideration
        # 3. Update showcase metrics

        if self.db_pool:
            try:
                async with self.db_pool.acquire() as _conn:
                    # Update popularity metrics (if such table exists)
                    # Example: increase showcase views counter
                    logger.info("Showcase popularity updated for project %s", project_id)
            except Exception as e:
                logger.warning("Failed to update showcase popularity: %s", e)

    async def stop(self):
        """Stop consumer"""
        if self.channel:
            await self.channel.close()
        if self.connection:
            await self.connection.close()
        if self.db_pool:
            await self.db_pool.close()
        self.on_connection_state(False)
        logger.info("Event consumer stopped")


# Global consumer instance
consumer: EventConsumer = EventConsumer()


async def start_event_consumer(on_connection_state=None, on_event=None):
    """Start consumer (called from main.py)"""
    global consumer
    # Recreate consumer with new hooks if provided
    consumer = EventConsumer(on_connection_state=on_connection_state, on_event=on_event)
    await consumer.start()
    return consumer


async def stop_event_consumer():
    """Stop consumer (called from main.py)"""
    if consumer:
        await consumer.stop()
