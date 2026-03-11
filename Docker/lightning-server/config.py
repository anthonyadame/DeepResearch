"""
Configuration module for Lightning Server
Handles environment variable loading and validation
"""
import os
import yaml
from pathlib import Path
from typing import Optional, Dict, Any, List
from pydantic import Field, ConfigDict
from pydantic_settings import BaseSettings


class MongoDBConfig(BaseSettings):
    """MongoDB connection configuration"""
    uri: str = Field(
        default="mongodb://localhost:27017/?replicaSet=rs0",
        description="MongoDB connection URI (replica set required)"
    )
    database: str = Field(
        default="agentlightning",
        description="Database name"
    )
    partition_id: str = Field(
        default="default",
        description="Partition ID for multi-trainer isolation"
    )
    max_pool_size: int = Field(
        default=100,
        description="Maximum connection pool size"
    )
    min_pool_size: int = Field(
        default=10,
        description="Minimum connection pool size"
    )
    connect_timeout: int = Field(
        default=30000,
        description="Connection timeout in milliseconds"
    )
    server_selection_timeout: int = Field(
        default=30000,
        description="Server selection timeout in milliseconds"
    )

    class Config:
        env_prefix = "MONGO_"
        case_sensitive = False

    def get_client_kwargs(self) -> Dict[str, Any]:
        """Get MongoDB client kwargs"""
        return {
            "maxPoolSize": self.max_pool_size,
            "minPoolSize": self.min_pool_size,
            "connectTimeoutMS": self.connect_timeout,
            "serverSelectionTimeoutMS": self.server_selection_timeout,
        }


class VLLMConfig(BaseSettings):
    """vLLM serving configuration"""
    model_config = ConfigDict(
        protected_namespaces=('settings_',),  # Fix Pydantic warning for model_ fields
        env_prefix='VLLM__',
        case_sensitive=False
    )

    api_base: str = Field(
        default="http://localhost:8000/v1",
        description="vLLM API base URL"
    )
    model_name: str = Field(
        default="llama-3.1-8b",
        description="Model name for vLLM"
    )
    api_key: str = Field(
        default="EMPTY",
        description="API key for vLLM (usually EMPTY for local)"
    )
    gpu_memory_utilization: float = Field(
        default=0.9,
        description="GPU memory utilization fraction"
    )
    max_model_len: int = Field(
        default=8192,
        description="Maximum model sequence length"
    )
    max_num_seqs: int = Field(
        default=256,
        description="Maximum number of concurrent sequences"
    )
    tensor_parallel_size: int = Field(
        default=1,
        description="Tensor parallelism size (number of GPUs)"
    )


class LLMProxyConfig(BaseSettings):
    """LLM Proxy configuration"""
    host: str = Field(
        default="0.0.0.0",
        description="LLM Proxy host"
    )
    port: int = Field(
        default=8080,
        description="LLM Proxy port"
    )
    routing_strategy: str = Field(
        default="usage-based-routing",
        description="LiteLLM routing strategy"
    )
    allowed_fails: int = Field(
        default=3,
        description="Allowed failures before cooldown"
    )
    cooldown_time: int = Field(
        default=60,
        description="Cooldown time in seconds"
    )

    class Config:
        env_prefix = "LLMPROXY_"
        case_sensitive = False


class OpenAIConfig(BaseSettings):
    """OpenAI API configuration"""
    api_key: Optional[str] = Field(
        default=None,
        description="OpenAI API key"
    )
    base_url: str = Field(
        default="https://api.openai.com/v1",
        description="OpenAI base URL"
    )

    class Config:
        env_prefix = "OPENAI_"
        case_sensitive = False


class VERLConfig(BaseSettings):
    """VERL training configuration"""
    model_config = ConfigDict(
        protected_namespaces=('settings_',),  # Fix Pydantic warning for model_ fields
        env_prefix='VERL__'
    )

    enabled: bool = Field(
        default=False,
        description="Enable VERL training"
    )
    config_file: Optional[str] = Field(
        default="/app/verl-config.yaml",
        description="Path to VERL YAML configuration file"
    )

    # Training hyperparameters
    train_batch_size: int = Field(
        default=4,
        description="Training batch size"
    )
    ppo_learning_rate: float = Field(
        default=1e-5,
        description="PPO learning rate"
    )
    ppo_epochs: int = Field(
        default=4,
        description="PPO epochs per batch"
    )
    ppo_clip_ratio: float = Field(
        default=0.2,
        description="PPO clipping parameter"
    )

    # Sequence lengths
    max_prompt_length: int = Field(
        default=512,
        description="Maximum prompt length"
    )
    max_response_length: int = Field(
        default=512,
        description="Maximum response length"
    )

    # Rollout settings
    n_rollouts: int = Field(
        default=16,
        description="Number of rollouts per input"
    )

    # Loss coefficients
    kl_coef: float = Field(
        default=0.05,
        description="KL divergence penalty coefficient"
    )
    value_loss_coef: float = Field(
        default=0.5,
        description="Value function loss coefficient"
    )
    entropy_coef: float = Field(
        default=0.01,
        description="Entropy bonus coefficient"
    )

    # Hardware
    n_gpus_per_node: int = Field(
        default=1,
        description="Number of GPUs per node"
    )

    # Model configuration
    model_path: str = Field(
        default="Qwen/Qwen3.5-2B-Instruct",
        description="Model path for VERL"
    )
    actor_model: str = Field(
        default="qwen3.5-2b",
        description="Actor model identifier"
    )
    vllm_endpoint: str = Field(
        default="http://localhost:8001",
        description="vLLM endpoint for model serving"
    )

    # Storage
    checkpoints_dir: str = Field(
        default="/app/verl_checkpoints",
        description="Directory for saving checkpoints"
    )
    mongodb_collection: str = Field(
        default="verl_training_jobs",
        description="MongoDB collection for training jobs"
    )

    # Training control
    num_train_steps: int = Field(
        default=1000,
        description="Total number of training steps"
    )
    save_interval: int = Field(
        default=100,
        description="Save checkpoint every N steps"
    )
    eval_interval: int = Field(
        default=50,
        description="Evaluate model every N steps"
    )

    # Ray distributed training
    ray_enabled: bool = Field(
        default=True,
        description="Enable Ray for distributed training"
    )

    tensor_parallel_size: int = Field(
        default=1,
        description="Tensor parallelism size"
    )

    # Loaded YAML configuration (populated at runtime)
    _yaml_config: Optional[Dict[str, Any]] = None

    def load_yaml_config(self) -> Dict[str, Any]:
        """Load configuration from YAML file"""
        if self._yaml_config is not None:
            return self._yaml_config

        config_path = Path(self.config_file) if self.config_file else None

        if config_path and config_path.exists():
            try:
                with open(config_path, 'r') as f:
                    self._yaml_config = yaml.safe_load(f)
                return self._yaml_config or {}
            except Exception as e:
                print(f"⚠️  Failed to load VERL config from {config_path}: {e}")
                return {}
        else:
            print(f"⚠️  VERL config file not found: {config_path}")
            return {}

    def get_training_config(self) -> Dict[str, Any]:
        """Get merged training configuration (YAML + env vars)"""
        yaml_conf = self.load_yaml_config()
        training_conf = yaml_conf.get('training', {})

        # Environment variables override YAML
        return {
            'ppo_learning_rate': self.ppo_learning_rate,
            'ppo_epochs': self.ppo_epochs,
            'ppo_clip_ratio': self.ppo_clip_ratio,
            'train_batch_size': self.train_batch_size,
            'n_rollouts': self.n_rollouts,
            'max_prompt_length': self.max_prompt_length,
            'max_response_length': self.max_response_length,
            'kl_coef': self.kl_coef,
            'value_loss_coef': self.value_loss_coef,
            'entropy_coef': self.entropy_coef,
            'num_train_steps': self.num_train_steps,
            'save_interval': self.save_interval,
            'eval_interval': self.eval_interval,
            **training_conf  # YAML config for additional params
        }

    def get_model_config(self) -> Dict[str, Any]:
        """Get model configuration from YAML"""
        yaml_conf = self.load_yaml_config()
        return yaml_conf.get('models', {})

    def get_hardware_config(self) -> Dict[str, Any]:
        """Get hardware configuration from YAML"""
        yaml_conf = self.load_yaml_config()
        hardware_conf = yaml_conf.get('hardware', {})

        return {
            'n_gpus_per_node': self.n_gpus_per_node,
            'ray_enabled': self.ray_enabled,
            'tensor_parallel_size': self.tensor_parallel_size,
            **hardware_conf
        }

    def get_storage_config(self) -> Dict[str, Any]:
        """Get storage configuration from YAML"""
        yaml_conf = self.load_yaml_config()
        storage_conf = yaml_conf.get('storage', {})

        return {
            'checkpoints_dir': self.checkpoints_dir,
            'mongodb_collection': self.mongodb_collection,
            **storage_conf
        }


class APOConfig(BaseSettings):
    """APO configuration"""
    model_config = ConfigDict(
        protected_namespaces=('settings_',),
        env_prefix='APO__'
    )

    enabled: bool = Field(
        default=False,
        description="Enable APO"
    )

    # MongoDB configuration
    mongodb_prompts_collection: str = Field(
        default="apo_prompts",
        description="MongoDB collection for prompts"
    )
    mongodb_runs_collection: str = Field(
        default="apo_optimization_runs",
        description="MongoDB collection for optimization runs"
    )

    # Optimization defaults
    default_iterations: int = Field(
        default=5,
        description="Default number of optimization iterations"
    )
    default_evaluation_samples: int = Field(
        default=10,
        description="Default number of evaluation samples per iteration"
    )
    optimization_strategy: str = Field(
        default="iterative_refinement",
        description="Default optimization strategy"
    )
    default_model: str = Field(
        default="Qwen/Qwen3.5-2B-Instruct",
        description="Default model for optimization"
    )

    # Evaluation criteria
    evaluation_criteria: List[str] = Field(
        default=["coherence", "relevance", "helpfulness"],
        description="Default evaluation criteria"
    )

    # LLM Evaluation configuration
    evaluation_use_llm: bool = Field(
        default=False,
        description="Use LLM for prompt evaluation instead of heuristics"
    )
    evaluation_llm_endpoint: str = Field(
        default="http://localhost:8000",
        description="LLM API endpoint for evaluation (vLLM/LiteLLM)"
    )
    evaluation_llm_model: str = Field(
        default="Qwen/Qwen3.5-2B-Instruct",
        description="Model to use for LLM-based evaluation"
    )
    evaluation_llm_timeout: int = Field(
        default=30,
        description="Timeout for LLM evaluation requests (seconds)"
    )
    evaluation_fallback_to_heuristic: bool = Field(
        default=True,
        description="Fall back to heuristic evaluation if LLM evaluation fails"
    )

    # Original APO fields (agentlightning library compatibility)
    gradient_model: str = Field(
        default="gpt-4o-mini",
        description="Model for computing gradients"
    )
    apply_edit_model: str = Field(
        default="gpt-4o-mini",
        description="Model for applying edits"
    )
    beam_width: int = Field(
        default=3,
        description="Beam search width (number of candidates to keep per iteration)"
    )
    variations_per_prompt: int = Field(
        default=2,
        description="Number of variations to generate per prompt in beam search"
    )

    # Genetic algorithm parameters
    population_size: int = Field(
        default=5,
        description="Population size for genetic algorithm"
    )
    mutation_rate: float = Field(
        default=0.3,
        description="Mutation probability for genetic algorithm (0.0-1.0)"
    )
    crossover_rate: float = Field(
        default=0.7,
        description="Crossover probability for genetic algorithm (0.0-1.0)"
    )
    tournament_size: int = Field(
        default=3,
        description="Tournament size for genetic algorithm selection"
    )

    branch_factor: int = Field(
        default=4,
        description="Number of variations per prompt"
    )
    beam_rounds: int = Field(
        default=3,
        description="Number of optimization rounds"
    )
    gradient_batch_size: int = Field(
        default=4,
        description="Gradient computation batch size"
    )
    val_batch_size: int = Field(
        default=16,
        description="Validation batch size"
    )
    diversity_temperature: float = Field(
        default=1.0,
        description="Temperature for diversity"
    )


class OpenTelemetryConfig(BaseSettings):
    """OpenTelemetry configuration"""
    enabled: bool = Field(
        default=True,
        description="Enable OpenTelemetry tracing"
    )
    otlp_endpoint: Optional[str] = Field(
        default="http://localhost:9090/v1/traces",
        description="OTLP HTTP endpoint (Lightning Store /v1/traces)"
    )
    export_timeout: int = Field(
        default=30,
        description="OTLP export timeout in seconds"
    )
    console_export: bool = Field(
        default=False,
        description="Enable console trace export (debug)"
    )
    service_name: str = Field(
        default="lightning-server",
        description="Service name for tracing"
    )

    class Config:
        env_prefix = "OTEL_"
        case_sensitive = False


class ServerConfig(BaseSettings):
    """Main server configuration"""
    lightning_port: int = Field(
        default=9090,
        description="Lightning server port"
    )
    prometheus_port: int = Field(
        default=9091,
        description="Prometheus metrics port"
    )
    prometheus_metrics_enabled: bool = Field(
        default=True,
        description="Enable Prometheus metrics"
    )
    log_level: str = Field(
        default="INFO",
        description="Logging level"
    )
    environment: str = Field(
        default="development",
        description="Deployment environment (development/staging/production)"
    )
    debug: bool = Field(
        default=False,
        description="Debug mode"
    )
    cors_allow_origins: str = Field(
        default="*",
        description="CORS allowed origins"
    )
    api_key: Optional[str] = Field(
        default=None,
        description="API authentication key"
    )

    # Legacy RMPT/RLCS settings (for backward compatibility)
    rmpt_enabled: bool = Field(
        default=True,
        description="Enable RMPT (legacy)"
    )
    rlcs_enabled: bool = Field(
        default=True,
        description="Enable RLCS (legacy)"
    )
    rmpt_strategy: str = Field(
        default="balanced",
        description="RMPT strategy (legacy)"
    )
    rmpt_max_tasks: int = Field(
        default=10,
        description="RMPT max tasks (legacy)"
    )
    rlcs_confidence_threshold: float = Field(
        default=0.7,
        description="RLCS confidence threshold (legacy)"
    )

    class Config:
        env_prefix = ""
        case_sensitive = False


class LightningServerConfig:
    """Central configuration manager"""

    def __init__(self):
        self.server = ServerConfig()
        self.mongodb = MongoDBConfig()
        self.vllm = VLLMConfig()
        self.llmproxy = LLMProxyConfig()
        self.openai = OpenAIConfig()
        self.verl = VERLConfig()
        self.apo = APOConfig()
        self.opentelemetry = OpenTelemetryConfig()

    @property
    def use_mongodb(self) -> bool:
        """Determine if MongoDB should be used (if URI is set and not default)"""
        return self.mongodb.uri != "mongodb://localhost:27017/?replicaSet=rs0" or \
               os.getenv("MONGO_URI") is not None

    @property
    def prometheus_enabled(self) -> bool:
        """Check if Prometheus metrics are enabled"""
        return os.getenv("PROMETHEUS_METRICS_ENABLED", "true").lower() == "true"
    
    def validate(self) -> None:
        """Validate configuration"""
        # Check MongoDB URI format if MongoDB is enabled
        if self.use_mongodb:
            if "replicaSet" not in self.mongodb.uri:
                raise ValueError(
                    "MongoDB replica set is required. "
                    "URI must include '?replicaSet=rs0' parameter"
                )
        
        # Validate VERL config if enabled
        if self.verl.enabled:
            if not self.use_mongodb:
                raise ValueError("VERL requires MongoDB for persistent storage")
        
        # Validate APO config if enabled
        if self.apo.enabled:
            if not self.openai.api_key and not self.vllm.api_base:
                raise ValueError("APO requires either OpenAI API key or vLLM endpoint")
    
    def get_info(self) -> Dict[str, Any]:
        """Get configuration info for debugging"""
        return {
            "storage": "mongodb" if self.use_mongodb else "in-memory",
            "mongodb_database": self.mongodb.database if self.use_mongodb else None,
            "mongodb_partition": self.mongodb.partition_id if self.use_mongodb else None,
            "vllm_enabled": bool(self.vllm.api_base),
            "verl_enabled": self.verl.enabled,
            "apo_enabled": self.apo.enabled,
            "prometheus_enabled": self.prometheus_enabled,
            "debug_mode": self.server.debug,
        }


# Global configuration instance
config = LightningServerConfig()
