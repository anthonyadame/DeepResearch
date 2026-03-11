# Security Module for Lightning Server
# API authentication, rate limiting, and input validation

import logging
import hashlib
import secrets
import time
from typing import Dict, Optional, Any
from datetime import datetime, timedelta
from collections import defaultdict

logger = logging.getLogger(__name__)


class APIKeyManager:
    """
    Manages API key authentication.
    """
    
    def __init__(self):
        """Initialize API key manager."""
        self.api_keys: Dict[str, Dict[str, Any]] = {}
        self._load_default_keys()
        
        logger.info("✅ API Key Manager initialized")
    
    def _load_default_keys(self):
        """Load default API keys from environment."""
        import os
        
        # Default admin key
        admin_key = os.getenv("ADMIN_API_KEY", self.generate_api_key())
        self.api_keys[admin_key] = {
            "name": "admin",
            "role": "admin",
            "created_at": datetime.utcnow().isoformat(),
            "rate_limit": 1000,  # requests per minute
            "enabled": True
        }
        
        logger.info(f"📝 Default admin API key loaded")
        logger.info(f"   Key: {admin_key[:8]}...{admin_key[-8:]}")
    
    def generate_api_key(self) -> str:
        """
        Generate a new API key.
        
        Returns:
            32-character API key
        """
        return secrets.token_hex(16)
    
    def create_key(
        self,
        name: str,
        role: str = "user",
        rate_limit: int = 100
    ) -> str:
        """
        Create a new API key.
        
        Args:
            name: Key identifier name
            role: User role (admin, user, readonly)
            rate_limit: Requests per minute
        
        Returns:
            Generated API key
        """
        api_key = self.generate_api_key()
        
        self.api_keys[api_key] = {
            "name": name,
            "role": role,
            "created_at": datetime.utcnow().isoformat(),
            "rate_limit": rate_limit,
            "enabled": True
        }
        
        logger.info(f"✅ API key created: {name} ({role})")
        return api_key
    
    def validate_key(self, api_key: str) -> Dict[str, Any]:
        """
        Validate API key.
        
        Args:
            api_key: API key to validate
        
        Returns:
            Dict with validation result and key info
        """
        if not api_key:
            return {
                "valid": False,
                "error": "API key missing"
            }
        
        key_info = self.api_keys.get(api_key)
        
        if not key_info:
            return {
                "valid": False,
                "error": "Invalid API key"
            }
        
        if not key_info["enabled"]:
            return {
                "valid": False,
                "error": "API key disabled"
            }
        
        return {
            "valid": True,
            "name": key_info["name"],
            "role": key_info["role"],
            "rate_limit": key_info["rate_limit"]
        }
    
    def revoke_key(self, api_key: str) -> bool:
        """
        Revoke (disable) an API key.
        
        Args:
            api_key: API key to revoke
        
        Returns:
            True if revoked, False if not found
        """
        if api_key in self.api_keys:
            self.api_keys[api_key]["enabled"] = False
            logger.info(f"🔒 API key revoked: {self.api_keys[api_key]['name']}")
            return True
        return False
    
    def list_keys(self) -> Dict[str, Any]:
        """
        List all API keys (masked).
        
        Returns:
            Dict with API key information
        """
        return {
            f"{key[:8]}...{key[-8:]}": {
                "name": info["name"],
                "role": info["role"],
                "rate_limit": info["rate_limit"],
                "enabled": info["enabled"],
                "created_at": info["created_at"]
            }
            for key, info in self.api_keys.items()
        }


class RateLimiter:
    """
    Token bucket rate limiter.
    
    Limits requests per API key or IP address.
    """
    
    def __init__(self, default_limit: int = 60):
        """
        Initialize rate limiter.
        
        Args:
            default_limit: Default requests per minute
        """
        self.default_limit = default_limit
        self.buckets: Dict[str, Dict[str, Any]] = defaultdict(
            lambda: {
                "tokens": default_limit,
                "last_update": time.time(),
                "limit": default_limit
            }
        )
        
        logger.info(f"✅ Rate Limiter initialized (default: {default_limit} req/min)")
    
    def check_rate_limit(
        self,
        identifier: str,
        limit: Optional[int] = None
    ) -> Dict[str, Any]:
        """
        Check if request is within rate limit.
        
        Args:
            identifier: API key or IP address
            limit: Custom rate limit (requests per minute)
        
        Returns:
            Dict with allowed status and remaining tokens
        """
        bucket = self.buckets[identifier]
        current_time = time.time()
        
        # Refill tokens based on elapsed time
        elapsed = current_time - bucket["last_update"]
        refill_rate = (limit or bucket["limit"]) / 60.0  # tokens per second
        bucket["tokens"] = min(
            (limit or bucket["limit"]),
            bucket["tokens"] + (elapsed * refill_rate)
        )
        bucket["last_update"] = current_time
        
        # Check if request allowed
        if bucket["tokens"] >= 1:
            bucket["tokens"] -= 1
            return {
                "allowed": True,
                "remaining": int(bucket["tokens"]),
                "limit": limit or bucket["limit"]
            }
        else:
            return {
                "allowed": False,
                "remaining": 0,
                "limit": limit or bucket["limit"],
                "retry_after": int(60.0 / refill_rate)
            }
    
    def reset_bucket(self, identifier: str):
        """
        Reset rate limit bucket for an identifier.
        
        Args:
            identifier: API key or IP address
        """
        if identifier in self.buckets:
            del self.buckets[identifier]
            logger.info(f"🔄 Rate limit bucket reset: {identifier[:16]}...")
    
    def get_status(self) -> Dict[str, Any]:
        """
        Get rate limiter status.
        
        Returns:
            Dict with active buckets and their status
        """
        return {
            "active_buckets": len(self.buckets),
            "default_limit": self.default_limit,
            "buckets": {
                f"{key[:16]}...": {
                    "tokens": int(bucket["tokens"]),
                    "limit": bucket["limit"]
                }
                for key, bucket in list(self.buckets.items())[:10]  # Show first 10
            }
        }


class InputValidator:
    """
    Input validation and sanitization.
    """
    
    @staticmethod
    def validate_prompt(prompt: str, max_length: int = 10000) -> Dict[str, Any]:
        """
        Validate prompt input.
        
        Args:
            prompt: User prompt
            max_length: Maximum allowed length
        
        Returns:
            Dict with validation result
        """
        if not prompt:
            return {
                "valid": False,
                "error": "Prompt cannot be empty"
            }
        
        if not isinstance(prompt, str):
            return {
                "valid": False,
                "error": "Prompt must be a string"
            }
        
        if len(prompt) > max_length:
            return {
                "valid": False,
                "error": f"Prompt exceeds maximum length ({max_length} characters)"
            }
        
        # Check for suspicious patterns
        suspicious_patterns = [
            "<script>",
            "javascript:",
            "onerror=",
            "onload=",
            "__import__",
            "eval(",
            "exec("
        ]
        
        prompt_lower = prompt.lower()
        for pattern in suspicious_patterns:
            if pattern in prompt_lower:
                logger.warning(f"⚠️  Suspicious pattern detected: {pattern}")
                return {
                    "valid": False,
                    "error": "Prompt contains suspicious patterns"
                }
        
        return {
            "valid": True,
            "sanitized_prompt": prompt.strip()
        }
    
    @staticmethod
    def validate_model_name(model: str) -> Dict[str, Any]:
        """
        Validate model name.
        
        Args:
            model: Model identifier
        
        Returns:
            Dict with validation result
        """
        if not model:
            return {
                "valid": False,
                "error": "Model name cannot be empty"
            }
        
        # Allow alphanumeric, hyphens, underscores, slashes, dots
        import re
        if not re.match(r'^[a-zA-Z0-9\-_/.]+$', model):
            return {
                "valid": False,
                "error": "Model name contains invalid characters"
            }
        
        if len(model) > 100:
            return {
                "valid": False,
                "error": "Model name too long"
            }
        
        return {
            "valid": True,
            "model": model
        }
    
    @staticmethod
    def validate_numeric_range(
        value: Any,
        name: str,
        min_val: float,
        max_val: float
    ) -> Dict[str, Any]:
        """
        Validate numeric value is within range.
        
        Args:
            value: Value to validate
            name: Parameter name
            min_val: Minimum allowed value
            max_val: Maximum allowed value
        
        Returns:
            Dict with validation result
        """
        try:
            num_value = float(value)
        except (TypeError, ValueError):
            return {
                "valid": False,
                "error": f"{name} must be a number"
            }
        
        if num_value < min_val or num_value > max_val:
            return {
                "valid": False,
                "error": f"{name} must be between {min_val} and {max_val}"
            }
        
        return {
            "valid": True,
            "value": num_value
        }


# Global instances
api_key_manager = APIKeyManager()
rate_limiter = RateLimiter(default_limit=60)
input_validator = InputValidator()


def get_security_status() -> Dict[str, Any]:
    """
    Get overall security status.
    
    Returns:
        Dict with security module status
    """
    return {
        "api_keys": {
            "total_keys": len(api_key_manager.api_keys),
            "active_keys": sum(1 for k in api_key_manager.api_keys.values() if k["enabled"])
        },
        "rate_limiter": rate_limiter.get_status(),
        "security_features": {
            "api_authentication": True,
            "rate_limiting": True,
            "input_validation": True,
            "suspicious_pattern_detection": True
        }
    }


if __name__ == "__main__":
    # Example usage
    print("Security Module")
    print("=" * 60)
    print()
    
    # Test API key
    test_key = api_key_manager.create_key("test_user", role="user", rate_limit=100)
    print(f"Generated API key: {test_key}")
    
    validation = api_key_manager.validate_key(test_key)
    print(f"Validation result: {validation}")
    print()
    
    # Test rate limiting
    for i in range(5):
        result = rate_limiter.check_rate_limit(test_key, limit=100)
        print(f"Request {i+1}: {result}")
    print()
    
    # Test input validation
    prompt_result = input_validator.validate_prompt("Hello, world!")
    print(f"Prompt validation: {prompt_result}")
    
    malicious_result = input_validator.validate_prompt("<script>alert('xss')</script>")
    print(f"Malicious prompt: {malicious_result}")
    print()
    
    print("✅ Security module loaded successfully")
