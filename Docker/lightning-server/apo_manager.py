# APO Manager - Agent Prompt Optimization
# Handles prompt optimization workflows, evaluation, and storage

import os
import logging
import asyncio
import json
from typing import Dict, List, Optional, Any
from datetime import datetime
from motor.motor_asyncio import AsyncIOMotorClient
import uuid

# HTTP client for LLM API calls
try:
    import httpx
    HTTPX_AVAILABLE = True
except ImportError:
    HTTPX_AVAILABLE = False
    httpx = None

# Beam search strategy
try:
    from beam_search import BeamSearchStrategy
    BEAM_SEARCH_AVAILABLE = True
except ImportError:
    BEAM_SEARCH_AVAILABLE = False
    BeamSearchStrategy = None

# Genetic algorithm strategy
try:
    from genetic_algorithm import GeneticAlgorithmStrategy
    GENETIC_ALGORITHM_AVAILABLE = True
except ImportError:
    GENETIC_ALGORITHM_AVAILABLE = False
    GeneticAlgorithmStrategy = None

# Strategy comparison framework
try:
    from strategy_comparison import StrategyComparison
    STRATEGY_COMPARISON_AVAILABLE = True
except ImportError:
    STRATEGY_COMPARISON_AVAILABLE = False
    StrategyComparison = None

logger = logging.getLogger(__name__)

# APO availability check
try:
    import agentlightning
    APO_AVAILABLE = True
    APO_VERSION = getattr(agentlightning, '__version__', 'unknown')
    logger.info(f"✅ agentlightning available (version: {APO_VERSION})")
except ImportError as e:
    APO_AVAILABLE = False
    APO_VERSION = None
    logger.warning(f"⚠️  APO not available: {e}")
    logger.warning("   Basic optimization will use fallback implementation")


class APOManager:
    """
    Manages Agent Prompt Optimization workflows.
    
    Provides:
    - Prompt optimization through iterative refinement
    - Performance evaluation and metrics
    - Version control for prompts
    - MongoDB persistence
    - LLM integration for evaluation
    """
    
    def __init__(
        self,
        config: Any,
        llm_client: Any = None,
        lightning_store: Any = None
    ):
        """
        Initialize APO Manager.
        
        Args:
            config: APOConfig instance
            llm_client: OpenAI-compatible LLM client (vLLM/LiteLLM)
            lightning_store: LightningStore for metrics tracking
        """
        self.config = config
        self.llm_client = llm_client
        self.lightning_store = lightning_store
        
        # MongoDB connection for APO data
        self.mongo_client = None
        self.mongo_db = None
        
        # Collections
        self.prompts_collection_name = getattr(config, 'mongodb_prompts_collection', 'apo_prompts')
        self.runs_collection_name = getattr(config, 'mongodb_runs_collection', 'apo_optimization_runs')
        
        # Initialize MongoDB connection
        try:
            from config import config as app_config
            if hasattr(app_config, 'mongodb') and hasattr(app_config.mongodb, 'uri'):
                mongo_uri = app_config.mongodb.uri
                db_name = app_config.mongodb.database
                self.mongo_client = AsyncIOMotorClient(mongo_uri)
                self.mongo_db = self.mongo_client[db_name]
                logger.info(f"✅ MongoDB connected for APO (db: {db_name})")
                logger.info(f"   Prompts collection: {self.prompts_collection_name}")
                logger.info(f"   Runs collection: {self.runs_collection_name}")
        except Exception as e:
            logger.warning(f"⚠️  APO MongoDB connection failed: {e}")
            logger.warning("   APO will operate without persistence")
        
        # Active optimization runs
        self.active_runs: Dict[str, Dict[str, Any]] = {}

        logger.info("✅ APO Manager initialized")
        logger.info(f"   APO available: {APO_AVAILABLE}")
        logger.info(f"   MongoDB: {self.mongo_db is not None}")
        logger.info(f"   LLM client: {self.llm_client is not None}")
    
    async def optimize_prompt(
        self,
        prompt_name: str,
        initial_prompt: str,
        domain: str = "general",
        description: Optional[str] = None,
        iterations: int = 5,
        evaluation_samples: int = 10,
        model: str = "Qwen/Qwen3.5-2B-Instruct",
        optimization_strategy: str = "iterative_refinement",
        evaluation_criteria: Optional[List[str]] = None,
        **kwargs
    ) -> Dict[str, Any]:
        """
        Optimize a prompt through iterative refinement.
        """
        try:
            # Generate run ID
            run_id = f"run_{datetime.utcnow().strftime('%Y%m%d_%H%M%S')}_{uuid.uuid4().hex[:8]}"
            
            # Create or get prompt record
            prompt_id = await self._get_or_create_prompt(
                prompt_name=prompt_name,
                initial_prompt=initial_prompt,
                domain=domain,
                description=description
            )
            
            # Initialize optimization run
            run_metadata = {
                "_id": run_id,
                "prompt_id": prompt_id,
                "prompt_name": prompt_name,
                "status": "pending",
                "config": {
                    "iterations": iterations,
                    "evaluation_samples": evaluation_samples,
                    "model": model,
                    "optimization_strategy": optimization_strategy,
                    "criteria": evaluation_criteria or ["coherence", "relevance", "helpfulness"]
                },
                "iterations_completed": [],
                "best_version": None,
                "best_score": 0.0,
                "improvement": 0.0,
                "created_at": datetime.utcnow().isoformat(),
                "started_at": None,
                "completed_at": None,
                "total_duration_seconds": 0
            }
            
            # Save to MongoDB
            if self.mongo_db is not None:
                collection = self.mongo_db[self.runs_collection_name]
                await collection.insert_one(run_metadata)
                logger.info(f"✅ Optimization run created: {run_id}")
            
            # Track active run
            self.active_runs[run_id] = {
                "start_time": datetime.utcnow(),
                "status": "pending"
            }
            
            # Start optimization in background
            asyncio.create_task(self._run_optimization(
                run_id=run_id,
                prompt_id=prompt_id,
                initial_prompt=initial_prompt,
                iterations=iterations,
                model=model,
                criteria=evaluation_criteria or ["coherence", "relevance", "helpfulness"],
                optimization_strategy=optimization_strategy
            ))
            
            return {
                "success": True,
                "run_id": run_id,
                "prompt_id": prompt_id,
                "status": "started",
                "iterations": iterations,
                "model": model
            }
        
        except Exception as e:
            logger.error(f"❌ Failed to start optimization: {e}", exc_info=True)
            return {
                "success": False,
                "error": str(e)
            }
    
    async def _run_optimization(
        self,
        run_id: str,
        prompt_id: str,
        initial_prompt: str,
        iterations: int,
        model: str,
        criteria: List[str],
        optimization_strategy: str = "iterative_refinement"
    ):
        """Background task to run optimization iterations."""
        try:
            # Update status to running
            if self.mongo_db is not None:
                collection = self.mongo_db[self.runs_collection_name]
                await collection.update_one(
                    {"_id": run_id},
                    {"$set": {
                        "status": "running",
                        "started_at": datetime.utcnow().isoformat()
                    }}
                )

            self.active_runs[run_id]["status"] = "running"

            # Route to appropriate optimization strategy
            if optimization_strategy == "beam_search" and BEAM_SEARCH_AVAILABLE:
                logger.info(f"🔍 Using BEAM SEARCH strategy for run {run_id}")
                await self._run_beam_search_optimization(
                    run_id=run_id,
                    prompt_id=prompt_id,
                    initial_prompt=initial_prompt,
                    iterations=iterations,
                    model=model,
                    criteria=criteria
                )
            elif optimization_strategy == "genetic_algorithm" and GENETIC_ALGORITHM_AVAILABLE:
                logger.info(f"🧬 Using GENETIC ALGORITHM strategy for run {run_id}")
                await self._run_genetic_algorithm_optimization(
                    run_id=run_id,
                    prompt_id=prompt_id,
                    initial_prompt=initial_prompt,
                    iterations=iterations,
                    model=model,
                    criteria=criteria
                )
            else:
                # Default to iterative refinement
                if optimization_strategy == "beam_search" and not BEAM_SEARCH_AVAILABLE:
                    logger.warning("⚠️  Beam search requested but not available, using iterative refinement")
                if optimization_strategy == "genetic_algorithm" and not GENETIC_ALGORITHM_AVAILABLE:
                    logger.warning("⚠️  Genetic algorithm requested but not available, using iterative refinement")
                logger.info(f"🔄 Using ITERATIVE REFINEMENT strategy for run {run_id}")
                await self._run_iterative_refinement(
                    run_id=run_id,
                    prompt_id=prompt_id,
                    initial_prompt=initial_prompt,
                    iterations=iterations,
                    model=model,
                    criteria=criteria
                )

        except Exception as e:
            logger.error(f"❌ Optimization failed for run {run_id}: {e}", exc_info=True)

            # Update run as failed
            if self.mongo_db is not None:
                collection = self.mongo_db[self.runs_collection_name]
                await collection.update_one(
                    {"_id": run_id},
                    {"$set": {
                        "status": "failed",
                        "error": str(e),
                        "completed_at": datetime.utcnow().isoformat()
                    }}
                )

            if run_id in self.active_runs:
                self.active_runs[run_id]["status"] = "failed"

    async def _run_iterative_refinement(
        self,
        run_id: str,
        prompt_id: str,
        initial_prompt: str,
        iterations: int,
        model: str,
        criteria: List[str]
    ):
        """Run iterative refinement optimization strategy."""
        current_prompt = initial_prompt
        current_score = 0.0
        iteration_results = []

        for i in range(iterations):
            logger.info(f"🔄 Optimization iteration {i+1}/{iterations} for run {run_id}")

            # Evaluate current prompt (use LLM if configured, otherwise heuristic)
            use_llm = getattr(self.config, 'evaluation_use_llm', False)

            if use_llm and HTTPX_AVAILABLE:
                llm_endpoint = getattr(self.config, 'evaluation_llm_endpoint', 'http://localhost:8000')
                llm_model = getattr(self.config, 'evaluation_llm_model', 'Qwen/Qwen3.5-2B-Instruct')

                evaluation = await self._evaluate_prompt_with_llm(
                    prompt=current_prompt,
                    model=model,
                    criteria=criteria,
                    llm_endpoint=llm_endpoint,
                    llm_model=llm_model
                )
                logger.info(f"   Using LLM evaluation (model: {llm_model})")
            else:
                evaluation = await self._evaluate_prompt_simple(
                    prompt=current_prompt,
                    model=model,
                    criteria=criteria
                )
                if use_llm and not HTTPX_AVAILABLE:
                    logger.warning("   LLM evaluation requested but httpx not available")

            iteration_result = {
                "iteration": i + 1,
                "prompt_version": i + 1,
                "prompt": current_prompt,
                "score": evaluation["score"],
                "metrics": evaluation["metrics"],
                "feedback": evaluation.get("feedback", ""),
                "duration_seconds": evaluation.get("duration", 0)
            }

            iteration_results.append(iteration_result)

            # Save iteration to MongoDB
            if self.mongo_db is not None:
                collection = self.mongo_db[self.runs_collection_name]
                await collection.update_one(
                    {"_id": run_id},
                    {"$push": {"iterations_completed": iteration_result}}
                )

            # Check if this is the best score
            if evaluation["score"] > current_score:
                current_score = evaluation["score"]

                # Update prompt in MongoDB
                await self._save_prompt_version(
                    prompt_id=prompt_id,
                    version=i + 1,
                    prompt_text=current_prompt,
                    score=evaluation["score"],
                    metrics=evaluation["metrics"]
                )

            # Generate improved prompt for next iteration
            if i < iterations - 1:
                current_prompt = await self._generate_improved_prompt(
                    current_prompt=current_prompt,
                    feedback=evaluation.get("feedback", ""),
                    model=model
                )

        # Calculate improvement
        initial_score = iteration_results[0]["score"] if iteration_results else 0
        final_score = iteration_results[-1]["score"] if iteration_results else 0
        improvement = final_score - initial_score

        # Update run as completed
        if self.mongo_db is not None:
            collection = self.mongo_db[self.runs_collection_name]
            await collection.update_one(
                {"_id": run_id},
                {"$set": {
                    "status": "completed",
                    "best_score": final_score,
                    "improvement": improvement,
                    "completed_at": datetime.utcnow().isoformat(),
                    "total_duration_seconds": (
                        datetime.utcnow() - self.active_runs[run_id]["start_time"]
                    ).total_seconds()
                }}
            )

        self.active_runs[run_id]["status"] = "completed"
        logger.info(f"✅ Iterative refinement completed: {run_id} (improvement: +{improvement:.2f})")

    async def _run_beam_search_optimization(
        self,
        run_id: str,
        prompt_id: str,
        initial_prompt: str,
        iterations: int,
        model: str,
        criteria: List[str]
    ):
        """Run beam search optimization strategy."""
        logger.info(f"🔍 Starting beam search optimization for run {run_id}")

        # Get beam search configuration
        beam_width = getattr(self.config, 'beam_width', 3)
        variations_per_prompt = getattr(self.config, 'variations_per_prompt', 2)

        # Create beam search strategy
        beam_search = BeamSearchStrategy(
            beam_width=beam_width,
            variations_per_prompt=variations_per_prompt,
            max_concurrent_evaluations=10
        )

        # Create evaluator function that uses our evaluation methods
        async def evaluator(prompt: str, model: str, criteria: List[str]) -> Dict[str, Any]:
            """Wrapper for evaluation methods compatible with beam search."""
            use_llm = getattr(self.config, 'evaluation_use_llm', False)

            if use_llm and HTTPX_AVAILABLE:
                llm_endpoint = getattr(self.config, 'evaluation_llm_endpoint', 'http://localhost:8000')
                llm_model = getattr(self.config, 'evaluation_llm_model', 'Qwen/Qwen3.5-2B-Instruct')

                return await self._evaluate_prompt_with_llm(
                    prompt=prompt,
                    model=model,
                    criteria=criteria,
                    llm_endpoint=llm_endpoint,
                    llm_model=llm_model
                )
            else:
                return await self._evaluate_prompt_simple(
                    prompt=prompt,
                    model=model,
                    criteria=criteria
                )

        # Run beam search
        result = await beam_search.run_beam_search(
            initial_prompt=initial_prompt,
            iterations=iterations,
            evaluator_func=evaluator,
            model=model,
            criteria=criteria
        )

        # Convert beam search results to APO iteration format
        iteration_results = []
        for iter_data in result["iterations"]:
            iteration_result = {
                "iteration": iter_data["iteration"],
                "prompt_version": iter_data["iteration"],
                "prompt": iter_data["best_prompt"],
                "score": iter_data["best_score"],
                "metrics": {
                    "beam_width": iter_data["beam_size"],
                    "candidates_evaluated": iter_data["candidates_evaluated"],
                    "beam_scores": iter_data["beam_scores"]
                },
                "feedback": f"Beam search iteration {iter_data['iteration']}: Evaluated {iter_data['candidates_evaluated']} candidates, kept top {iter_data['beam_size']}. Best score: {iter_data['best_score']:.3f}, Beam: {iter_data['beam_scores']}",
                "duration_seconds": iter_data["duration_seconds"]
            }
            iteration_results.append(iteration_result)

            # Save iteration to MongoDB
            if self.mongo_db is not None:
                collection = self.mongo_db[self.runs_collection_name]
                await collection.update_one(
                    {"_id": run_id},
                    {"$push": {"iterations_completed": iteration_result}}
                )

            # Save best prompt version
            await self._save_prompt_version(
                prompt_id=prompt_id,
                version=iter_data["iteration"],
                prompt_text=iter_data["best_prompt"],
                score=iter_data["best_score"],
                metrics=iteration_result["metrics"]
            )

        # Update run as completed
        if self.mongo_db is not None:
            collection = self.mongo_db[self.runs_collection_name]
            await collection.update_one(
                {"_id": run_id},
                {"$set": {
                    "status": "completed",
                    "best_score": result["best_score"],
                    "improvement": result["improvement"],
                    "completed_at": datetime.utcnow().isoformat(),
                    "total_duration_seconds": result["total_duration_seconds"],
                    "strategy_metadata": {
                        "beam_width": result["beam_width"],
                        "variations_per_prompt": result["variations_per_prompt"]
                    }
                }}
            )

        self.active_runs[run_id]["status"] = "completed"
        logger.info(
            f"✅ Beam search completed: {run_id} "
            f"(best_score: {result['best_score']:.3f}, improvement: +{result['improvement']:.3f})"
        )

    async def _evaluate_prompt_simple(
        self,
        prompt: str,
        model: str,
        criteria: List[str]
    ) -> Dict[str, Any]:
        """Simple prompt evaluation using heuristics."""
        metrics = {}
        
        word_count = len(prompt.split())
        
        if "coherence" in criteria:
            metrics["coherence"] = min(0.95, 0.5 + (word_count / 100))
        
        if "relevance" in criteria:
            has_context = any(word in prompt.lower() for word in ["you are", "assistant", "help"])
            metrics["relevance"] = 0.85 if has_context else 0.60
        
        if "helpfulness" in criteria:
            has_action = any(word in prompt.lower() for word in ["provide", "help", "assist", "answer"])
            metrics["helpfulness"] = 0.90 if has_action else 0.65
        
        score = sum(metrics.values()) / len(metrics) if metrics else 0.5

        return {
            "score": round(score, 3),
            "metrics": metrics,
            "feedback": "Baseline evaluation using heuristics",
            "duration": 0.1
        }

    async def _evaluate_prompt_with_llm(
        self,
        prompt: str,
        model: str,
        criteria: List[str],
        llm_endpoint: str = "http://localhost:8000",
        llm_model: str = "Qwen/Qwen3.5-2B-Instruct"
    ) -> Dict[str, Any]:
        """
        Evaluate prompt using LLM-based assessment.

        Args:
            prompt: The prompt template to evaluate
            model: Model being optimized for (informational)
            criteria: List of evaluation criteria
            llm_endpoint: LLM API endpoint
            llm_model: Model to use for evaluation

        Returns:
            Dictionary with score, metrics, feedback, and duration
        """
        if not HTTPX_AVAILABLE:
            logger.warning("httpx not available, falling back to heuristic evaluation")
            return await self._evaluate_prompt_simple(prompt, model, criteria)

        start_time = datetime.utcnow()

        try:
            # Build evaluation prompt
            evaluation_prompt = self._build_evaluation_prompt(prompt, criteria)

            # Call LLM API
            async with httpx.AsyncClient(timeout=30.0) as client:
                response = await client.post(
                    f"{llm_endpoint}/v1/completions",
                    json={
                        "model": llm_model,
                        "prompt": evaluation_prompt,
                        "max_tokens": 500,
                        "temperature": 0.3,
                        "stop": ["###"]
                    },
                    headers={"Content-Type": "application/json"}
                )

                if response.status_code != 200:
                    logger.warning(f"LLM evaluation failed with status {response.status_code}, using fallback")
                    return await self._evaluate_prompt_simple(prompt, model, criteria)

                result = response.json()
                evaluation_text = result.get("choices", [{}])[0].get("text", "").strip()

                # Parse evaluation results
                metrics = self._parse_llm_evaluation(evaluation_text, criteria)

                score = sum(metrics.values()) / len(metrics) if metrics else 0.5

                duration = (datetime.utcnow() - start_time).total_seconds()

                return {
                    "score": round(score, 3),
                    "metrics": metrics,
                    "feedback": evaluation_text,
                    "duration": duration,
                    "evaluation_method": "llm"
                }

        except Exception as e:
            logger.warning(f"LLM evaluation error: {e}, falling back to heuristic")
            return await self._evaluate_prompt_simple(prompt, model, criteria)

    def _build_evaluation_prompt(self, prompt: str, criteria: List[str]) -> str:
        """Build comprehensive evaluation prompt for LLM."""
        criteria_descriptions = {
            "coherence": "Coherence: Does the prompt have clear structure, logical flow, and proper grammar?",
            "relevance": "Relevance: Is the prompt appropriate for its intended use case and domain?",
            "helpfulness": "Helpfulness: Does the prompt provide clear guidance for actionable responses?"
        }

        criteria_text = "\n".join([
            f"- {criteria_descriptions.get(c, c)}"
            for c in criteria
        ])

        evaluation_prompt = f"""You are an expert at evaluating AI prompts. Evaluate the following prompt template across multiple criteria.

PROMPT TO EVALUATE:
{prompt}

EVALUATION CRITERIA:
{criteria_text}

For each criterion, provide a score from 0.0 to 1.0 (where 1.0 is perfect).

Output format (JSON):
{{
  "coherence": 0.85,
  "relevance": 0.90,
  "helpfulness": 0.88,
  "overall_assessment": "Brief explanation of strengths and weaknesses"
}}

Evaluation:
"""
        return evaluation_prompt

    def _parse_llm_evaluation(self, evaluation_text: str, criteria: List[str]) -> Dict[str, float]:
        """Parse LLM evaluation response to extract scores."""
        metrics = {}

        try:
            # Try to parse JSON from the response
            json_start = evaluation_text.find('{')
            json_end = evaluation_text.rfind('}') + 1

            if json_start >= 0 and json_end > json_start:
                json_str = evaluation_text[json_start:json_end]
                parsed = json.loads(json_str)

                for criterion in criteria:
                    if criterion in parsed:
                        score = float(parsed[criterion])
                        metrics[criterion] = max(0.0, min(1.0, score))

            # If parsing failed or missing criteria, extract from text
            for criterion in criteria:
                if criterion not in metrics:
                    # Look for patterns like "coherence: 0.85" or "coherence score: 0.85"
                    import re
                    pattern = rf'{criterion}[:\s]+([0-9.]+)'
                    match = re.search(pattern, evaluation_text.lower())
                    if match:
                        score = float(match.group(1))
                        metrics[criterion] = max(0.0, min(1.0, score))
                    else:
                        # Default to 0.7 if not found
                        metrics[criterion] = 0.7

        except Exception as e:
            logger.warning(f"Failed to parse LLM evaluation: {e}")
            # Fall back to default scores
            for criterion in criteria:
                metrics[criterion] = 0.7

        return metrics

    async def _generate_improved_prompt(
        self,
        current_prompt: str,
        feedback: str,
        model: str
    ) -> str:
        """Generate an improved version of the prompt."""
        if len(current_prompt.split()) < 50:
            improved = current_prompt + "\n\nProvide clear, concise, and helpful responses."
        else:
            improved = current_prompt

        return improved
    
    async def _get_or_create_prompt(
        self,
        prompt_name: str,
        initial_prompt: str,
        domain: str,
        description: Optional[str]
    ) -> str:
        """Get existing prompt or create new one."""
        prompt_id = f"prompt_{datetime.utcnow().strftime('%Y%m%d_%H%M%S')}_{uuid.uuid4().hex[:8]}"
        
        if self.mongo_db is not None:
            collection = self.mongo_db[self.prompts_collection_name]
            
            existing = await collection.find_one({"name": prompt_name})
            
            if existing:
                return existing["_id"]
            
            prompt_doc = {
                "_id": prompt_id,
                "name": prompt_name,
                "description": description or "",
                "domain": domain,
                "versions": [],
                "current_version": 0,
                "optimization_runs": 0,
                "created_at": datetime.utcnow().isoformat(),
                "last_optimized": None
            }
            
            await collection.insert_one(prompt_doc)
            logger.info(f"✅ Created new prompt: {prompt_id} ({prompt_name})")
        
        return prompt_id
    
    async def _save_prompt_version(
        self,
        prompt_id: str,
        version: int,
        prompt_text: str,
        score: float,
        metrics: Dict[str, float]
    ):
        """Save a new version of a prompt."""
        if self.mongo_db is None:
            return
        
        collection = self.mongo_db[self.prompts_collection_name]
        
        version_doc = {
            "version": version,
            "template": prompt_text,
            "created_at": datetime.utcnow().isoformat(),
            "performance_score": score,
            "metrics": metrics
        }
        
        await collection.update_one(
            {"_id": prompt_id},
            {
                "$push": {"versions": version_doc},
                "$set": {
                    "current_version": version,
                    "last_optimized": datetime.utcnow().isoformat()
                },
                "$inc": {"optimization_runs": 1}
            }
        )
    
    async def list_prompts(
        self,
        limit: int = 10,
        domain: Optional[str] = None
    ) -> Dict[str, Any]:
        """List optimized prompts."""
        try:
            if self.mongo_db is None:
                return {"success": False, "error": "MongoDB not available"}
            
            collection = self.mongo_db[self.prompts_collection_name]
            query = {}
            if domain:
                query["domain"] = domain
            
            cursor = collection.find(query).sort("last_optimized", -1).limit(limit)
            prompts = await cursor.to_list(length=limit)
            
            return {"success": True, "prompts": prompts, "count": len(prompts)}
        
        except Exception as e:
            logger.error(f"❌ Failed to list prompts: {e}", exc_info=True)
            return {"success": False, "error": str(e)}
    
    async def list_runs(
        self,
        limit: int = 10,
        status: Optional[str] = None
    ) -> Dict[str, Any]:
        """List optimization runs."""
        try:
            if self.mongo_db is None:
                return {"success": False, "error": "MongoDB not available"}
            
            collection = self.mongo_db[self.runs_collection_name]
            query = {}
            if status:
                query["status"] = status
            
            cursor = collection.find(query).sort("created_at", -1).limit(limit)
            runs = await cursor.to_list(length=limit)
            
            return {"success": True, "runs": runs, "count": len(runs)}
        
        except Exception as e:
            logger.error(f"❌ Failed to list runs: {e}", exc_info=True)
            return {"success": False, "error": str(e)}

    async def _run_genetic_algorithm_optimization(
        self,
        run_id: str,
        prompt_id: str,
        initial_prompt: str,
        iterations: int,
        model: str,
        criteria: List[str]
    ):
        """Run genetic algorithm optimization strategy."""
        logger.info(f"🧬 Starting genetic algorithm optimization for run {run_id}")

        # Get genetic algorithm configuration
        population_size = getattr(self.config, 'population_size', 5)
        mutation_rate = getattr(self.config, 'mutation_rate', 0.3)
        crossover_rate = getattr(self.config, 'crossover_rate', 0.7)
        tournament_size = getattr(self.config, 'tournament_size', 3)

        logger.info(
            f"GA Config: population={population_size}, mutation={mutation_rate}, "
            f"crossover={crossover_rate}, tournament={tournament_size}"
        )

        # Create genetic algorithm strategy instance
        ga_strategy = GeneticAlgorithmStrategy(
            population_size=population_size,
            mutation_rate=mutation_rate,
            crossover_rate=crossover_rate,
            tournament_size=tournament_size,
            max_concurrent_evaluations=10
        )

        # Determine evaluator function
        use_llm = getattr(self.config, 'evaluation_use_llm', False)
        llm_endpoint = getattr(self.config, 'evaluation_llm_endpoint', 'http://localhost:8000')
        llm_model = getattr(self.config, 'evaluation_llm_model', 'Qwen/Qwen3.5-2B-Instruct')
        llm_timeout = getattr(self.config, 'evaluation_llm_timeout', 30)
        fallback_to_heuristic = getattr(self.config, 'evaluation_fallback_to_heuristic', True)

        logger.info(f"Evaluation mode: {'LLM' if use_llm else 'Heuristic'}")

        # Define evaluator wrapper function for GA
        async def evaluator(prompt: str, model: str, criteria: List[str]) -> Dict[str, Any]:
            """Wrapper to evaluate prompt using configured method."""
            if use_llm and HTTPX_AVAILABLE:
                try:
                    result = await self._evaluate_prompt_with_llm(
                        prompt=prompt,
                        model=model,
                        criteria=criteria,
                        llm_endpoint=llm_endpoint,
                        llm_model=llm_model,
                        timeout=llm_timeout
                    )
                    return result
                except Exception as e:
                    logger.warning(f"LLM evaluation failed: {e}")
                    if fallback_to_heuristic:
                        logger.info("Falling back to heuristic evaluation")
                        return await self._evaluate_prompt_simple(prompt, model, criteria)
                    else:
                        raise
            else:
                return await self._evaluate_prompt_simple(prompt, model, criteria)

        # Run genetic algorithm (use iterations as generations)
        try:
            result = await ga_strategy.run_genetic_algorithm(
                initial_prompt=initial_prompt,
                generations=iterations,
                evaluator_func=evaluator,
                model=model,
                criteria=criteria
            )

            # Convert GA results to APO iteration format
            iterations_results = []

            for gen_idx, gen_data in enumerate(result["generations"]):
                iteration_result = {
                    "iteration": gen_idx + 1,
                    "generation": gen_idx + 1,
                    "best_score": gen_data["best_score"],
                    "avg_score": gen_data["avg_score"],
                    "population_diversity": gen_data["population_diversity"],
                    "duration_seconds": gen_data["duration_seconds"],
                    "timestamp": datetime.utcnow().isoformat()
                }
                iterations_results.append(iteration_result)

                # Save prompt version for this generation
                if self.mongo_db is not None:
                    await self._save_prompt_version(
                        prompt_id=prompt_id,
                        version=gen_idx + 1,
                        prompt_text=result["final_population"][0][0] if gen_idx == len(result["generations"]) - 1 else f"Gen {gen_idx + 1} best",
                        score=gen_data["best_score"],
                        metrics={"avg_score": gen_data["avg_score"], "diversity": gen_data["population_diversity"]}
                    )

            # Save final results to MongoDB
            if self.mongo_db is not None:
                collection = self.mongo_db[self.runs_collection_name]
                await collection.update_one(
                    {"_id": run_id},
                    {"$set": {
                        "status": "completed",
                        "best_score": result["best_score"],
                        "best_prompt": result["best_prompt"],
                        "iterations_completed": iterations_results,
                        "total_duration_seconds": result["total_duration"],
                        "final_population_size": len(result["final_population"]),
                        "strategy_metadata": {
                            "population_size": population_size,
                            "mutation_rate": mutation_rate,
                            "crossover_rate": crossover_rate,
                            "tournament_size": tournament_size
                        },
                        "completed_at": datetime.utcnow().isoformat()
                    }}
                )

            # Update in-memory state
            if run_id in self.active_runs:
                self.active_runs[run_id].update({
                    "status": "completed",
                    "best_score": result["best_score"],
                    "best_prompt": result["best_prompt"],
                    "iterations_completed": iterations
                })

            logger.info(
                f"✅ Genetic algorithm completed: {run_id} "
                f"(best_score={result['best_score']:.3f}, "
                f"final_diversity={result['generations'][-1]['population_diversity']}/{population_size})"
            )

        except Exception as e:
            logger.error(f"❌ Genetic algorithm failed: {e}", exc_info=True)

            if self.mongo_db is not None:
                collection = self.mongo_db[self.runs_collection_name]
                await collection.update_one(
                    {"_id": run_id},
                    {"$set": {
                        "status": "failed",
                        "error": str(e),
                        "completed_at": datetime.utcnow().isoformat()
                    }}
                )

            if run_id in self.active_runs:
                self.active_runs[run_id]["status"] = "failed"

            raise

    async def get_run_status(self, run_id: str) -> Dict[str, Any]:
        """Get detailed status of an optimization run."""
        try:
            if self.mongo_db is None:
                return {"success": False, "error": "MongoDB not available"}

            collection = self.mongo_db[self.runs_collection_name]
            run = await collection.find_one({"_id": run_id})

            if not run:
                return {"success": False, "error": f"Run not found: {run_id}"}

            return {"success": True, "run": run}

        except Exception as e:
            logger.error(f"❌ Failed to get run status: {e}", exc_info=True)
            return {"success": False, "error": str(e)}

    async def compare_strategies(
        self,
        strategies: List[str],
        initial_prompt: str,
        iterations: int = 5,
        domain: str = "general",
        criteria: Optional[List[str]] = None,
        model: str = "gpt-4",
        priority: str = "balanced",
        max_duration_seconds: Optional[float] = None,
        min_quality_score: Optional[float] = None
    ) -> Dict[str, Any]:
        """
        Compare multiple optimization strategies side-by-side.

        Args:
            strategies: List of strategy names to compare (e.g., ["iterative_refinement", "beam_search", "genetic_algorithm"])
            initial_prompt: Starting prompt for all strategies
            iterations: Number of iterations per strategy
            domain: Domain for optimization
            criteria: Evaluation criteria
            model: Model to use
            priority: "speed", "quality", "balanced", or "robustness"
            max_duration_seconds: Maximum allowed duration
            min_quality_score: Minimum required quality score

        Returns:
            Comparison results with metrics and recommendation
        """
        try:
            if not STRATEGY_COMPARISON_AVAILABLE:
                return {
                    "success": False,
                    "error": "Strategy comparison framework not available"
                }

            logger.info(
                f"📊 Starting strategy comparison: {strategies} "
                f"(iterations={iterations}, priority={priority})"
            )

            # Create comparison instance
            from strategy_comparison import StrategyRequirements

            requirements = StrategyRequirements(
                priority=priority,
                max_duration_seconds=max_duration_seconds,
                min_quality_score=min_quality_score,
                use_llm_evaluation=getattr(self.config, 'evaluation_use_llm', False)
            )

            comparison = StrategyComparison(self)

            # Run comparison
            result = await comparison.compare_strategies(
                strategies=strategies,
                initial_prompt=initial_prompt,
                iterations=iterations,
                domain=domain,
                criteria=criteria,
                model=model,
                requirements=requirements
            )

            # Save to MongoDB if available
            if self.mongo_db is not None and result.get("success"):
                collection = self.mongo_db["apo_strategy_comparisons"]
                comparison_doc = {
                    "_id": result["comparison_id"],
                    **result,
                    "created_at": datetime.utcnow().isoformat()
                }
                await collection.insert_one(comparison_doc)
                logger.info(f"✅ Comparison saved: {result['comparison_id']}")

            return result

        except Exception as e:
            logger.error(f"❌ Strategy comparison failed: {e}", exc_info=True)
            return {
                "success": False,
                "error": str(e)
            }
