"""
VERL Model Architectures - Advanced Model Configurations
=========================================================

Provides sophisticated transformer architectures and custom reward models
for reinforcement learning training. Supports:

- Multi-layer transformer configurations
- Custom reward model architectures (transformer, MLP, hybrid)
- Policy and value networks for actor-critic methods
- Flexible architecture composition

Author: DeepResearch Lightning Server
Date: March 9, 2026
Progress: 95% → 98% (VERL Advanced Features)
"""

from dataclasses import dataclass, field
from typing import Optional, List, Dict, Literal, Any
from enum import Enum
import logging

logger = logging.getLogger(__name__)


# ══════════════════════════════════════════════════════════════════════════════
# CONFIGURATION CLASSES
# ══════════════════════════════════════════════════════════════════════════════

@dataclass
class TransformerConfig:
    """
    Advanced transformer architecture configuration.
    
    Supports customization of all major transformer hyperparameters
    for policy and value networks in VERL training.
    
    Attributes:
        num_layers: Number of transformer layers
        num_heads: Number of attention heads per layer
        hidden_size: Dimension of hidden states
        intermediate_size: Dimension of feed-forward intermediate layer
        max_position_embeddings: Maximum sequence length
        vocab_size: Size of vocabulary
        dropout: Dropout probability for hidden states
        attention_dropout: Dropout probability for attention weights
        activation_function: Activation function (gelu, relu, swish)
        layer_norm_epsilon: Epsilon for layer normalization
        use_cache: Whether to use key-value caching for inference
        gradient_checkpointing: Trade memory for compute during training
    """
    num_layers: int = 12
    num_heads: int = 12
    hidden_size: int = 768
    intermediate_size: int = 3072
    max_position_embeddings: int = 2048
    vocab_size: int = 50257
    dropout: float = 0.1
    attention_dropout: float = 0.1
    activation_function: str = "gelu"
    layer_norm_epsilon: float = 1e-5
    use_cache: bool = True
    gradient_checkpointing: bool = False
    
    def __post_init__(self):
        """Validate configuration after initialization"""
        # Validate hidden_size is divisible by num_heads
        if self.hidden_size % self.num_heads != 0:
            raise ValueError(
                f"hidden_size ({self.hidden_size}) must be divisible by "
                f"num_heads ({self.num_heads})"
            )
        
        # Validate activation function
        valid_activations = ["gelu", "relu", "swish", "silu"]
        if self.activation_function not in valid_activations:
            raise ValueError(
                f"activation_function must be one of {valid_activations}, "
                f"got {self.activation_function}"
            )
    
    @property
    def head_size(self) -> int:
        """Calculate attention head dimension"""
        return self.hidden_size // self.num_heads
    
    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for serialization"""
        return {
            "num_layers": self.num_layers,
            "num_heads": self.num_heads,
            "hidden_size": self.hidden_size,
            "intermediate_size": self.intermediate_size,
            "max_position_embeddings": self.max_position_embeddings,
            "vocab_size": self.vocab_size,
            "dropout": self.dropout,
            "attention_dropout": self.attention_dropout,
            "activation_function": self.activation_function,
            "layer_norm_epsilon": self.layer_norm_epsilon,
            "use_cache": self.use_cache,
            "gradient_checkpointing": self.gradient_checkpointing,
        }


class RewardModelArchitecture(str, Enum):
    """Supported reward model architecture types"""
    TRANSFORMER = "transformer"
    MLP = "mlp"
    HYBRID = "hybrid"  # Transformer encoder + MLP head
    CUSTOM = "custom"


@dataclass
class RewardModelConfig:
    """
    Custom reward model configuration.
    
    Supports multiple architecture types for learning reward functions
    from human feedback or other signals.
    
    Attributes:
        architecture: Architecture type (transformer, mlp, hybrid, custom)
        input_size: Dimension of input features
        hidden_sizes: List of hidden layer dimensions for MLP
        output_size: Dimension of output (typically 1 for scalar reward)
        activation: Activation function for hidden layers
        use_layer_norm: Whether to use layer normalization
        dropout: Dropout probability
        transformer_config: Transformer config if using transformer architecture
    """
    architecture: RewardModelArchitecture = RewardModelArchitecture.TRANSFORMER
    input_size: int = 768
    hidden_sizes: List[int] = field(default_factory=lambda: [512, 256, 128])
    output_size: int = 1  # Scalar reward
    activation: str = "relu"
    use_layer_norm: bool = True
    dropout: float = 0.1
    transformer_config: Optional[TransformerConfig] = None
    
    def __post_init__(self):
        """Validate and initialize transformer config if needed"""
        if self.architecture == RewardModelArchitecture.TRANSFORMER:
            if self.transformer_config is None:
                # Create default transformer config
                self.transformer_config = TransformerConfig(
                    num_layers=6,  # Smaller than policy network
                    num_heads=8,
                    hidden_size=self.input_size,
                    intermediate_size=self.input_size * 4
                )
        
        # Validate activation
        valid_activations = ["relu", "gelu", "tanh", "sigmoid"]
        if self.activation not in valid_activations:
            raise ValueError(
                f"activation must be one of {valid_activations}, "
                f"got {self.activation}"
            )
    
    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for serialization"""
        result = {
            "architecture": self.architecture.value,
            "input_size": self.input_size,
            "hidden_sizes": self.hidden_sizes,
            "output_size": self.output_size,
            "activation": self.activation,
            "use_layer_norm": self.use_layer_norm,
            "dropout": self.dropout,
        }
        if self.transformer_config:
            result["transformer_config"] = self.transformer_config.to_dict()
        return result


@dataclass
class PolicyNetworkConfig:
    """
    Configuration for policy network in actor-critic methods.
    
    The policy network learns the action distribution (e.g., next token
    probabilities) conditioned on the current state.
    
    Attributes:
        transformer_config: Underlying transformer configuration
        action_space_size: Size of action space (vocab_size for language)
        temperature: Sampling temperature for action selection
        top_k: Top-k filtering for sampling
        top_p: Nucleus (top-p) filtering for sampling
    """
    transformer_config: TransformerConfig = field(default_factory=TransformerConfig)
    action_space_size: Optional[int] = None  # Defaults to vocab_size
    temperature: float = 1.0
    top_k: int = 50
    top_p: float = 0.95
    
    def __post_init__(self):
        """Initialize action_space_size from vocab_size if not set"""
        if self.action_space_size is None:
            self.action_space_size = self.transformer_config.vocab_size
    
    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for serialization"""
        return {
            "transformer_config": self.transformer_config.to_dict(),
            "action_space_size": self.action_space_size,
            "temperature": self.temperature,
            "top_k": self.top_k,
            "top_p": self.top_p,
        }


@dataclass
class ValueNetworkConfig:
    """
    Configuration for value network in actor-critic methods.
    
    The value network estimates the expected return (value) from the
    current state, used as baseline in policy gradient methods.
    
    Attributes:
        transformer_config: Underlying transformer configuration
        output_activation: Activation for final value prediction (none, tanh)
        value_clip_range: Clip value predictions to this range
    """
    transformer_config: TransformerConfig = field(default_factory=TransformerConfig)
    output_activation: Optional[str] = None  # None or "tanh"
    value_clip_range: Optional[tuple] = None  # (min, max) or None
    
    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for serialization"""
        return {
            "transformer_config": self.transformer_config.to_dict(),
            "output_activation": self.output_activation,
            "value_clip_range": self.value_clip_range,
        }


# ══════════════════════════════════════════════════════════════════════════════
# MODEL ARCHITECTURE CLASSES
# ══════════════════════════════════════════════════════════════════════════════

class PolicyNetwork:
    """
    Advanced policy network with transformer backbone.
    
    Learns the policy π(a|s) for selecting actions given states.
    In language modeling context, predicts next token distribution.
    
    This is a placeholder for integration with VERL's actual training.
    The real implementation would use PyTorch/JAX and integrate with
    the VERL PPO training loop.
    """
    
    def __init__(self, config: PolicyNetworkConfig):
        self.config = config
        self.transformer_config = config.transformer_config
        self.action_space_size = config.action_space_size
        
        logger.info(
            f"Initializing PolicyNetwork: "
            f"{self.transformer_config.num_layers} layers, "
            f"{self.transformer_config.num_heads} heads, "
            f"hidden_size={self.transformer_config.hidden_size}, "
            f"action_space={self.action_space_size}"
        )
    
    def get_architecture_summary(self) -> Dict[str, Any]:
        """Get summary of network architecture"""
        return {
            "type": "PolicyNetwork",
            "num_layers": self.transformer_config.num_layers,
            "num_heads": self.transformer_config.num_heads,
            "hidden_size": self.transformer_config.hidden_size,
            "intermediate_size": self.transformer_config.intermediate_size,
            "action_space_size": self.action_space_size,
            "dropout": self.transformer_config.dropout,
            "gradient_checkpointing": self.transformer_config.gradient_checkpointing,
        }
    
    def estimate_parameters(self) -> int:
        """Estimate total number of parameters"""
        h = self.transformer_config.hidden_size
        i = self.transformer_config.intermediate_size
        v = self.transformer_config.vocab_size
        n = self.transformer_config.num_layers
        
        # Embedding layer
        params = v * h
        
        # Per-layer parameters
        per_layer = (
            # Self-attention (Q, K, V, O projections)
            4 * h * h +
            # Feed-forward (2 linear layers)
            2 * h * i +
            # Layer norm (2 per layer)
            4 * h
        )
        params += n * per_layer
        
        # Output layer (token prediction)
        params += h * v
        
        return params


class ValueNetwork:
    """
    Value network for critic in actor-critic setup.
    
    Estimates V(s), the expected return from state s.
    Used as baseline to reduce variance in policy gradients.
    
    Similar architecture to policy network but outputs scalar values
    instead of action distributions.
    """
    
    def __init__(self, config: ValueNetworkConfig):
        self.config = config
        self.transformer_config = config.transformer_config
        
        logger.info(
            f"Initializing ValueNetwork: "
            f"{self.transformer_config.num_layers} layers, "
            f"{self.transformer_config.num_heads} heads, "
            f"hidden_size={self.transformer_config.hidden_size}"
        )
    
    def get_architecture_summary(self) -> Dict[str, Any]:
        """Get summary of network architecture"""
        return {
            "type": "ValueNetwork",
            "num_layers": self.transformer_config.num_layers,
            "num_heads": self.transformer_config.num_heads,
            "hidden_size": self.transformer_config.hidden_size,
            "intermediate_size": self.transformer_config.intermediate_size,
            "output_activation": self.config.output_activation,
            "value_clip_range": self.config.value_clip_range,
            "dropout": self.transformer_config.dropout,
        }
    
    def estimate_parameters(self) -> int:
        """Estimate total number of parameters"""
        h = self.transformer_config.hidden_size
        i = self.transformer_config.intermediate_size
        v = self.transformer_config.vocab_size
        n = self.transformer_config.num_layers
        
        # Embedding layer
        params = v * h
        
        # Per-layer parameters
        per_layer = (
            # Self-attention
            4 * h * h +
            # Feed-forward
            2 * h * i +
            # Layer norm
            4 * h
        )
        params += n * per_layer
        
        # Value head (single scalar output)
        params += h  # Linear projection to scalar
        
        return params


class CustomRewardModel:
    """
    Flexible reward model supporting multiple architectures.
    
    Learns reward function r(s, a) from human feedback or other signals.
    Supports transformer, MLP, and hybrid architectures.
    """
    
    def __init__(self, config: RewardModelConfig):
        self.config = config
        self.architecture = config.architecture
        
        logger.info(
            f"Initializing CustomRewardModel: "
            f"architecture={self.architecture.value}, "
            f"input_size={config.input_size}, "
            f"output_size={config.output_size}"
        )
    
    def get_architecture_summary(self) -> Dict[str, Any]:
        """Get summary of reward model architecture"""
        summary = {
            "type": "CustomRewardModel",
            "architecture": self.architecture.value,
            "input_size": self.config.input_size,
            "output_size": self.config.output_size,
            "activation": self.config.activation,
            "use_layer_norm": self.config.use_layer_norm,
            "dropout": self.config.dropout,
        }
        
        if self.architecture == RewardModelArchitecture.MLP:
            summary["hidden_sizes"] = self.config.hidden_sizes
        elif self.architecture in [RewardModelArchitecture.TRANSFORMER, RewardModelArchitecture.HYBRID]:
            if self.config.transformer_config:
                summary["transformer_layers"] = self.config.transformer_config.num_layers
                summary["transformer_heads"] = self.config.transformer_config.num_heads
        
        return summary
    
    def estimate_parameters(self) -> int:
        """Estimate total number of parameters"""
        if self.architecture == RewardModelArchitecture.MLP:
            # MLP parameters
            params = 0
            prev_size = self.config.input_size
            for hidden_size in self.config.hidden_sizes:
                params += prev_size * hidden_size + hidden_size  # Weights + bias
                if self.config.use_layer_norm:
                    params += 2 * hidden_size  # Layer norm gamma + beta
                prev_size = hidden_size
            # Output layer
            params += prev_size * self.config.output_size + self.config.output_size
            return params
        
        elif self.architecture == RewardModelArchitecture.TRANSFORMER:
            # Transformer parameters
            if self.config.transformer_config:
                h = self.config.transformer_config.hidden_size
                i = self.config.transformer_config.intermediate_size
                n = self.config.transformer_config.num_layers
                
                params = (
                    # Per-layer
                    n * (4 * h * h + 2 * h * i + 4 * h) +
                    # Output head
                    h * self.config.output_size
                )
                return params
        
        elif self.architecture == RewardModelArchitecture.HYBRID:
            # Transformer encoder + MLP head
            transformer_params = 0
            if self.config.transformer_config:
                h = self.config.transformer_config.hidden_size
                i = self.config.transformer_config.intermediate_size
                n = self.config.transformer_config.num_layers
                transformer_params = n * (4 * h * h + 2 * h * i + 4 * h)
            
            # MLP head
            mlp_params = 0
            prev_size = self.config.input_size
            for hidden_size in self.config.hidden_sizes:
                mlp_params += prev_size * hidden_size + hidden_size
                prev_size = hidden_size
            mlp_params += prev_size * self.config.output_size
            
            return transformer_params + mlp_params
        
        return 0  # Unknown architecture


# ══════════════════════════════════════════════════════════════════════════════
# FACTORY FUNCTIONS
# ══════════════════════════════════════════════════════════════════════════════

def create_small_policy_network() -> PolicyNetwork:
    """Create small policy network for testing/development"""
    config = PolicyNetworkConfig(
        transformer_config=TransformerConfig(
            num_layers=6,
            num_heads=8,
            hidden_size=512,
            intermediate_size=2048,
            vocab_size=50257
        )
    )
    return PolicyNetwork(config)


def create_medium_policy_network() -> PolicyNetwork:
    """Create medium policy network (similar to GPT-2 small)"""
    config = PolicyNetworkConfig(
        transformer_config=TransformerConfig(
            num_layers=12,
            num_heads=12,
            hidden_size=768,
            intermediate_size=3072,
            vocab_size=50257
        )
    )
    return PolicyNetwork(config)


def create_large_policy_network() -> PolicyNetwork:
    """Create large policy network (similar to GPT-2 medium)"""
    config = PolicyNetworkConfig(
        transformer_config=TransformerConfig(
            num_layers=24,
            num_heads=16,
            hidden_size=1024,
            intermediate_size=4096,
            vocab_size=50257
        )
    )
    return PolicyNetwork(config)


def create_value_network(policy_config: PolicyNetworkConfig) -> ValueNetwork:
    """Create value network matching policy network architecture"""
    config = ValueNetworkConfig(
        transformer_config=policy_config.transformer_config
    )
    return ValueNetwork(config)


def create_transformer_reward_model() -> CustomRewardModel:
    """Create transformer-based reward model"""
    config = RewardModelConfig(
        architecture=RewardModelArchitecture.TRANSFORMER,
        input_size=768,
        output_size=1,
        transformer_config=TransformerConfig(
            num_layers=6,
            num_heads=8,
            hidden_size=768,
            intermediate_size=3072
        )
    )
    return CustomRewardModel(config)


def create_mlp_reward_model() -> CustomRewardModel:
    """Create MLP-based reward model"""
    config = RewardModelConfig(
        architecture=RewardModelArchitecture.MLP,
        input_size=768,
        hidden_sizes=[512, 256, 128],
        output_size=1,
        activation="relu",
        use_layer_norm=True
    )
    return CustomRewardModel(config)


# ══════════════════════════════════════════════════════════════════════════════
# UTILITY FUNCTIONS
# ══════════════════════════════════════════════════════════════════════════════

def format_parameter_count(count: int) -> str:
    """Format parameter count in human-readable form"""
    if count >= 1_000_000_000:
        return f"{count / 1_000_000_000:.2f}B"
    elif count >= 1_000_000:
        return f"{count / 1_000_000:.2f}M"
    elif count >= 1_000:
        return f"{count / 1_000:.2f}K"
    else:
        return str(count)


def get_model_memory_estimate(num_parameters: int, dtype_bytes: int = 4) -> str:
    """
    Estimate model memory usage.
    
    Args:
        num_parameters: Total number of model parameters
        dtype_bytes: Bytes per parameter (4 for float32, 2 for float16)
    
    Returns:
        Formatted memory estimate (e.g., "2.5 GB")
    """
    total_bytes = num_parameters * dtype_bytes
    
    # Convert to appropriate unit
    if total_bytes >= 1_073_741_824:  # 1 GB
        return f"{total_bytes / 1_073_741_824:.2f} GB"
    elif total_bytes >= 1_048_576:  # 1 MB
        return f"{total_bytes / 1_048_576:.2f} MB"
    elif total_bytes >= 1024:  # 1 KB
        return f"{total_bytes / 1024:.2f} KB"
    else:
        return f"{total_bytes} bytes"


if __name__ == "__main__":
    # Example usage and validation
    
    # Create small policy network
    print("=" * 70)
    print("SMALL POLICY NETWORK")
    print("=" * 70)
    small_policy = create_small_policy_network()
    summary = small_policy.get_architecture_summary()
    params = small_policy.estimate_parameters()
    print(f"Architecture: {summary}")
    print(f"Parameters: {format_parameter_count(params)} ({params:,})")
    print(f"Memory (FP32): {get_model_memory_estimate(params, 4)}")
    print(f"Memory (FP16): {get_model_memory_estimate(params, 2)}")
    
    # Create medium policy network
    print("\n" + "=" * 70)
    print("MEDIUM POLICY NETWORK")
    print("=" * 70)
    medium_policy = create_medium_policy_network()
    summary = medium_policy.get_architecture_summary()
    params = medium_policy.estimate_parameters()
    print(f"Architecture: {summary}")
    print(f"Parameters: {format_parameter_count(params)} ({params:,})")
    print(f"Memory (FP32): {get_model_memory_estimate(params, 4)}")
    
    # Create value network
    print("\n" + "=" * 70)
    print("VALUE NETWORK (matching medium policy)")
    print("=" * 70)
    value_net = create_value_network(medium_policy.config)
    summary = value_net.get_architecture_summary()
    params = value_net.estimate_parameters()
    print(f"Architecture: {summary}")
    print(f"Parameters: {format_parameter_count(params)} ({params:,})")
    
    # Create transformer reward model
    print("\n" + "=" * 70)
    print("TRANSFORMER REWARD MODEL")
    print("=" * 70)
    transformer_reward = create_transformer_reward_model()
    summary = transformer_reward.get_architecture_summary()
    params = transformer_reward.estimate_parameters()
    print(f"Architecture: {summary}")
    print(f"Parameters: {format_parameter_count(params)} ({params:,})")
    
    # Create MLP reward model
    print("\n" + "=" * 70)
    print("MLP REWARD MODEL")
    print("=" * 70)
    mlp_reward = create_mlp_reward_model()
    summary = mlp_reward.get_architecture_summary()
    params = mlp_reward.estimate_parameters()
    print(f"Architecture: {summary}")
    print(f"Parameters: {format_parameter_count(params)} ({params:,})")
    
    print("\n✅ All model architectures initialized successfully!")
