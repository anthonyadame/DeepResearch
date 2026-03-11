# Error Recovery Module for Lightning Server
# Implements retry logic, circuit breakers, and graceful degradation

import logging
import time
import asyncio
from typing import Callable, Any, Optional, Dict
from enum import Enum
from datetime import datetime, timedelta

logger = logging.getLogger(__name__)


class CircuitState(Enum):
    """Circuit breaker states."""
    CLOSED = "closed"  # Normal operation
    OPEN = "open"  # Failing, rejecting requests
    HALF_OPEN = "half_open"  # Testing if service recovered


class CircuitBreaker:
    """
    Circuit breaker pattern for external service calls.
    
    Prevents cascading failures by stopping requests to failing services
    and allowing periodic retry attempts.
    """
    
    def __init__(
        self,
        name: str,
        failure_threshold: int = 5,
        recovery_timeout: int = 60,
        expected_exception: type = Exception
    ):
        """
        Initialize circuit breaker.
        
        Args:
            name: Circuit breaker name (for logging)
            failure_threshold: Number of failures before opening circuit
            recovery_timeout: Seconds before attempting recovery
            expected_exception: Exception type to catch
        """
        self.name = name
        self.failure_threshold = failure_threshold
        self.recovery_timeout = recovery_timeout
        self.expected_exception = expected_exception
        
        self.failure_count = 0
        self.last_failure_time: Optional[datetime] = None
        self.state = CircuitState.CLOSED
        
        logger.info(f"✅ Circuit breaker initialized: {name}")
        logger.info(f"   Failure threshold: {failure_threshold}")
        logger.info(f"   Recovery timeout: {recovery_timeout}s")
    
    def call(self, func: Callable, *args, **kwargs) -> Any:
        """
        Call function with circuit breaker protection.
        
        Args:
            func: Function to call
            *args: Positional arguments
            **kwargs: Keyword arguments
        
        Returns:
            Function result
        
        Raises:
            Exception: If circuit is open or function fails
        """
        if self.state == CircuitState.OPEN:
            if self._should_attempt_reset():
                logger.info(f"🔄 Circuit breaker {self.name}: Attempting recovery (HALF_OPEN)")
                self.state = CircuitState.HALF_OPEN
            else:
                raise Exception(f"Circuit breaker {self.name} is OPEN")
        
        try:
            result = func(*args, **kwargs)
            self._on_success()
            return result
        
        except self.expected_exception as e:
            self._on_failure()
            raise e
    
    async def call_async(self, func: Callable, *args, **kwargs) -> Any:
        """
        Call async function with circuit breaker protection.
        
        Args:
            func: Async function to call
            *args: Positional arguments
            **kwargs: Keyword arguments
        
        Returns:
            Function result
        
        Raises:
            Exception: If circuit is open or function fails
        """
        if self.state == CircuitState.OPEN:
            if self._should_attempt_reset():
                logger.info(f"🔄 Circuit breaker {self.name}: Attempting recovery (HALF_OPEN)")
                self.state = CircuitState.HALF_OPEN
            else:
                raise Exception(f"Circuit breaker {self.name} is OPEN")
        
        try:
            result = await func(*args, **kwargs)
            self._on_success()
            return result
        
        except self.expected_exception as e:
            self._on_failure()
            raise e
    
    def _on_success(self):
        """Handle successful call."""
        if self.state == CircuitState.HALF_OPEN:
            logger.info(f"✅ Circuit breaker {self.name}: Recovery successful (CLOSED)")
            self.state = CircuitState.CLOSED
        
        self.failure_count = 0
    
    def _on_failure(self):
        """Handle failed call."""
        self.failure_count += 1
        self.last_failure_time = datetime.utcnow()
        
        logger.warning(f"⚠️  Circuit breaker {self.name}: Failure {self.failure_count}/{self.failure_threshold}")
        
        if self.failure_count >= self.failure_threshold:
            logger.error(f"❌ Circuit breaker {self.name}: OPEN (too many failures)")
            self.state = CircuitState.OPEN
    
    def _should_attempt_reset(self) -> bool:
        """Check if enough time has passed to attempt reset."""
        if self.last_failure_time is None:
            return True
        
        time_since_failure = (datetime.utcnow() - self.last_failure_time).total_seconds()
        return time_since_failure >= self.recovery_timeout
    
    def get_status(self) -> Dict[str, Any]:
        """Get circuit breaker status."""
        return {
            "name": self.name,
            "state": self.state.value,
            "failure_count": self.failure_count,
            "last_failure": self.last_failure_time.isoformat() if self.last_failure_time else None
        }


class RetryPolicy:
    """
    Retry policy with exponential backoff.
    
    Automatically retries failed operations with increasing delays.
    """
    
    def __init__(
        self,
        max_attempts: int = 3,
        base_delay: float = 1.0,
        max_delay: float = 60.0,
        exponential_base: float = 2.0
    ):
        """
        Initialize retry policy.
        
        Args:
            max_attempts: Maximum retry attempts
            base_delay: Initial delay in seconds
            max_delay: Maximum delay in seconds
            exponential_base: Multiplier for exponential backoff
        """
        self.max_attempts = max_attempts
        self.base_delay = base_delay
        self.max_delay = max_delay
        self.exponential_base = exponential_base
    
    def execute(self, func: Callable, *args, **kwargs) -> Any:
        """
        Execute function with retry logic.
        
        Args:
            func: Function to execute
            *args: Positional arguments
            **kwargs: Keyword arguments
        
        Returns:
            Function result
        
        Raises:
            Exception: If all retry attempts fail
        """
        last_exception = None
        
        for attempt in range(1, self.max_attempts + 1):
            try:
                logger.info(f"🔄 Retry attempt {attempt}/{self.max_attempts}")
                result = func(*args, **kwargs)
                
                if attempt > 1:
                    logger.info(f"✅ Retry succeeded on attempt {attempt}")
                
                return result
            
            except Exception as e:
                last_exception = e
                
                if attempt < self.max_attempts:
                    delay = min(
                        self.base_delay * (self.exponential_base ** (attempt - 1)),
                        self.max_delay
                    )
                    
                    logger.warning(f"⚠️  Attempt {attempt} failed: {e}")
                    logger.warning(f"   Retrying in {delay:.1f}s...")
                    time.sleep(delay)
                else:
                    logger.error(f"❌ All {self.max_attempts} retry attempts failed")
        
        raise last_exception
    
    async def execute_async(self, func: Callable, *args, **kwargs) -> Any:
        """
        Execute async function with retry logic.
        
        Args:
            func: Async function to execute
            *args: Positional arguments
            **kwargs: Keyword arguments
        
        Returns:
            Function result
        
        Raises:
            Exception: If all retry attempts fail
        """
        last_exception = None
        
        for attempt in range(1, self.max_attempts + 1):
            try:
                logger.info(f"🔄 Retry attempt {attempt}/{self.max_attempts}")
                result = await func(*args, **kwargs)
                
                if attempt > 1:
                    logger.info(f"✅ Retry succeeded on attempt {attempt}")
                
                return result
            
            except Exception as e:
                last_exception = e
                
                if attempt < self.max_attempts:
                    delay = min(
                        self.base_delay * (self.exponential_base ** (attempt - 1)),
                        self.max_delay
                    )
                    
                    logger.warning(f"⚠️  Attempt {attempt} failed: {e}")
                    logger.warning(f"   Retrying in {delay:.1f}s...")
                    await asyncio.sleep(delay)
                else:
                    logger.error(f"❌ All {self.max_attempts} retry attempts failed")
        
        raise last_exception


# Global circuit breakers for key services
circuit_breakers: Dict[str, CircuitBreaker] = {
    "mongodb": CircuitBreaker(
        name="MongoDB",
        failure_threshold=5,
        recovery_timeout=30
    ),
    "vllm": CircuitBreaker(
        name="vLLM",
        failure_threshold=3,
        recovery_timeout=60
    ),
    "qdrant": CircuitBreaker(
        name="Qdrant",
        failure_threshold=5,
        recovery_timeout=30
    ),
    "ollama": CircuitBreaker(
        name="Ollama",
        failure_threshold=3,
        recovery_timeout=60
    )
}


def get_circuit_breaker(service: str) -> Optional[CircuitBreaker]:
    """
    Get circuit breaker for a service.
    
    Args:
        service: Service name
    
    Returns:
        CircuitBreaker instance or None
    """
    return circuit_breakers.get(service)


def get_all_circuit_breaker_status() -> Dict[str, Any]:
    """
    Get status of all circuit breakers.
    
    Returns:
        Dict with circuit breaker statuses
    """
    return {
        name: cb.get_status()
        for name, cb in circuit_breakers.items()
    }


# Default retry policies
retry_policies: Dict[str, RetryPolicy] = {
    "default": RetryPolicy(
        max_attempts=3,
        base_delay=1.0,
        max_delay=10.0
    ),
    "aggressive": RetryPolicy(
        max_attempts=5,
        base_delay=0.5,
        max_delay=5.0
    ),
    "conservative": RetryPolicy(
        max_attempts=2,
        base_delay=2.0,
        max_delay=30.0
    )
}


def get_retry_policy(policy_name: str = "default") -> RetryPolicy:
    """
    Get retry policy by name.
    
    Args:
        policy_name: Policy name ('default', 'aggressive', 'conservative')
    
    Returns:
        RetryPolicy instance
    """
    return retry_policies.get(policy_name, retry_policies["default"])


if __name__ == "__main__":
    # Example usage
    print("Error Recovery Module")
    print("=" * 60)
    print()
    
    print("Circuit Breakers:")
    for name, cb in circuit_breakers.items():
        status = cb.get_status()
        print(f"  - {name}: {status['state']}")
    print()
    
    print("Retry Policies:")
    for name, policy in retry_policies.items():
        print(f"  - {name}: max_attempts={policy.max_attempts}, base_delay={policy.base_delay}s")
    print()
    
    print("✅ Error recovery module loaded successfully")
