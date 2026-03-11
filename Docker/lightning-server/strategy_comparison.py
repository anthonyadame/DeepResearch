"""
Strategy Comparison Framework for APO (Agent Prompt Optimization)

Enables side-by-side comparison of all optimization strategies:
- Iterative Refinement (single-path, fast)
- Beam Search (multi-path, quality-focused)
- Genetic Algorithm (population-based, robust)

Features:
- Parallel execution of multiple strategies
- Performance metrics collection
- Cost/quality trade-off analysis
- Automatic strategy recommendation
"""

import logging
import asyncio
from typing import List, Dict, Any, Optional
from datetime import datetime
from dataclasses import dataclass

logger = logging.getLogger(__name__)


@dataclass
class StrategyRequirements:
    """Requirements for strategy selection."""
    priority: str = "balanced"  # "speed", "quality", "balanced", "robustness"
    max_duration_seconds: Optional[float] = None
    min_quality_score: Optional[float] = None
    max_evaluations: Optional[int] = None
    use_llm_evaluation: bool = False


@dataclass
class StrategyResult:
    """Results from a single strategy execution."""
    strategy_name: str
    run_id: str
    status: str
    best_score: float
    best_prompt: str
    duration_seconds: float
    iterations_completed: int
    total_evaluations: int
    metadata: Dict[str, Any]
    error: Optional[str] = None


class StrategyComparison:
    """
    Compare multiple APO optimization strategies side-by-side.
    
    Executes strategies in parallel and provides:
    - Performance metrics comparison
    - Cost/quality trade-off analysis
    - Strategy recommendations based on requirements
    """
    
    def __init__(self, apo_manager):
        """
        Initialize strategy comparison framework.
        
        Args:
            apo_manager: APOManager instance for running optimizations
        """
        self.apo_manager = apo_manager
        self.available_strategies = ["iterative_refinement"]
        
        # Check which strategies are available
        try:
            from beam_search import BeamSearchStrategy
            self.available_strategies.append("beam_search")
            logger.info("✅ Beam search strategy available")
        except ImportError:
            logger.warning("⚠️  Beam search not available")
        
        try:
            from genetic_algorithm import GeneticAlgorithmStrategy
            self.available_strategies.append("genetic_algorithm")
            logger.info("✅ Genetic algorithm strategy available")
        except ImportError:
            logger.warning("⚠️  Genetic algorithm not available")
        
        logger.info(f"📊 Strategy comparison initialized with {len(self.available_strategies)} strategies")
    
    async def compare_strategies(
        self,
        strategies: List[str],
        initial_prompt: str,
        iterations: int = 5,
        domain: str = "general",
        criteria: Optional[List[str]] = None,
        model: str = "gpt-4",
        requirements: Optional[StrategyRequirements] = None
    ) -> Dict[str, Any]:
        """
        Compare multiple strategies side-by-side.
        
        Args:
            strategies: List of strategy names to compare
            initial_prompt: Starting prompt for all strategies
            iterations: Number of iterations/generations per strategy
            domain: Domain for prompt optimization
            criteria: Evaluation criteria
            model: Model to use for optimization
            requirements: Optional requirements for recommendations
            
        Returns:
            Comparison results with metrics and recommendation
        """
        if criteria is None:
            criteria = ["clarity", "specificity", "effectiveness"]
        
        if requirements is None:
            requirements = StrategyRequirements()
        
        # Validate strategies
        invalid_strategies = [s for s in strategies if s not in self.available_strategies]
        if invalid_strategies:
            logger.warning(f"⚠️  Unavailable strategies: {invalid_strategies}")
            strategies = [s for s in strategies if s in self.available_strategies]
        
        if not strategies:
            return {
                "success": False,
                "error": "No valid strategies to compare",
                "available_strategies": self.available_strategies
            }
        
        logger.info(
            f"🏁 Starting comparison of {len(strategies)} strategies: {strategies}"
        )
        
        start_time = datetime.utcnow()
        
        # Run all strategies in parallel
        tasks = []
        for strategy in strategies:
            task = self._run_strategy(
                strategy=strategy,
                initial_prompt=initial_prompt,
                iterations=iterations,
                domain=domain,
                criteria=criteria,
                model=model
            )
            tasks.append(task)
        
        # Wait for all to complete
        results = await asyncio.gather(*tasks, return_exceptions=True)
        
        # Process results
        strategy_results = []
        for i, result in enumerate(results):
            if isinstance(result, Exception):
                logger.error(f"❌ Strategy {strategies[i]} failed: {result}")
                strategy_results.append(StrategyResult(
                    strategy_name=strategies[i],
                    run_id="",
                    status="failed",
                    best_score=0.0,
                    best_prompt="",
                    duration_seconds=0.0,
                    iterations_completed=0,
                    total_evaluations=0,
                    metadata={},
                    error=str(result)
                ))
            else:
                strategy_results.append(result)
        
        total_duration = (datetime.utcnow() - start_time).total_seconds()
        
        # Generate comparison metrics
        comparison = self._generate_comparison_metrics(strategy_results)
        
        # Generate recommendation
        recommendation = self._generate_recommendation(
            strategy_results,
            requirements
        )
        
        logger.info(
            f"✅ Comparison complete in {total_duration:.1f}s. "
            f"Recommended: {recommendation['recommended_strategy']}"
        )
        
        return {
            "success": True,
            "comparison_id": f"comp_{datetime.utcnow().strftime('%Y%m%d_%H%M%S')}",
            "timestamp": datetime.utcnow().isoformat(),
            "total_duration_seconds": total_duration,
            "strategies_compared": len(strategy_results),
            "initial_prompt": initial_prompt,
            "iterations": iterations,
            "results": [self._result_to_dict(r) for r in strategy_results],
            "comparison": comparison,
            "recommendation": recommendation
        }
    
    async def _run_strategy(
        self,
        strategy: str,
        initial_prompt: str,
        iterations: int,
        domain: str,
        criteria: List[str],
        model: str
    ) -> StrategyResult:
        """
        Run a single optimization strategy.
        
        Args:
            strategy: Strategy name
            initial_prompt: Starting prompt
            iterations: Number of iterations
            domain: Domain
            criteria: Evaluation criteria
            model: Model name
            
        Returns:
            StrategyResult with performance data
        """
        start_time = datetime.utcnow()
        
        try:
            # Create optimization run
            prompt_name = f"compare_{strategy}_{datetime.utcnow().strftime('%H%M%S')}"
            
            result = await self.apo_manager.optimize_prompt(
                prompt_name=prompt_name,
                initial_prompt=initial_prompt,
                domain=domain,
                iterations=iterations,
                model=model,
                criteria=criteria,
                optimization_strategy=strategy
            )
            
            if not result.get("success"):
                raise Exception(result.get("error", "Unknown error"))
            
            run_id = result["run_id"]
            
            # Wait for completion (with timeout)
            max_wait = 300  # 5 minutes
            wait_interval = 2
            elapsed = 0
            
            while elapsed < max_wait:
                await asyncio.sleep(wait_interval)
                elapsed += wait_interval
                
                status_result = await self.apo_manager.get_run_status(run_id)
                if not status_result.get("success"):
                    continue
                
                run_data = status_result["run"]
                if run_data["status"] in ["completed", "failed"]:
                    break
            
            # Get final status
            final_status = await self.apo_manager.get_run_status(run_id)
            if not final_status.get("success"):
                raise Exception("Failed to get final status")
            
            run_data = final_status["run"]
            duration = (datetime.utcnow() - start_time).total_seconds()
            
            # Calculate total evaluations
            total_evals = self._calculate_evaluations(
                strategy,
                iterations,
                run_data.get("strategy_metadata", {})
            )
            
            return StrategyResult(
                strategy_name=strategy,
                run_id=run_id,
                status=run_data.get("status", "unknown"),
                best_score=run_data.get("best_score", 0.0),
                best_prompt=run_data.get("best_prompt", ""),
                duration_seconds=duration,
                iterations_completed=len(run_data.get("iterations_completed", [])),
                total_evaluations=total_evals,
                metadata=run_data.get("strategy_metadata", {})
            )
        
        except Exception as e:
            logger.error(f"❌ Strategy {strategy} failed: {e}", exc_info=True)
            duration = (datetime.utcnow() - start_time).total_seconds()
            
            return StrategyResult(
                strategy_name=strategy,
                run_id="",
                status="failed",
                best_score=0.0,
                best_prompt="",
                duration_seconds=duration,
                iterations_completed=0,
                total_evaluations=0,
                metadata={},
                error=str(e)
            )
    
    def _calculate_evaluations(
        self,
        strategy: str,
        iterations: int,
        metadata: Dict[str, Any]
    ) -> int:
        """Calculate total evaluations for a strategy."""
        if strategy == "iterative_refinement":
            return iterations
        
        elif strategy == "beam_search":
            beam_width = metadata.get("beam_width", 3)
            variations = metadata.get("variations_per_prompt", 2)
            return beam_width * variations * iterations
        
        elif strategy == "genetic_algorithm":
            population_size = metadata.get("population_size", 5)
            return population_size * iterations
        
        return iterations
    
    def _generate_comparison_metrics(
        self,
        results: List[StrategyResult]
    ) -> Dict[str, Any]:
        """Generate comparison metrics across strategies."""
        successful_results = [r for r in results if r.status == "completed"]
        
        if not successful_results:
            return {"error": "No successful strategy executions"}
        
        # Find best in each category
        best_quality = max(successful_results, key=lambda r: r.best_score)
        fastest = min(successful_results, key=lambda r: r.duration_seconds)
        most_efficient = min(
            successful_results,
            key=lambda r: r.total_evaluations if r.total_evaluations > 0 else float('inf')
        )
        
        # Calculate quality/speed trade-off
        quality_speed_ratios = []
        for r in successful_results:
            if r.duration_seconds > 0:
                ratio = r.best_score / r.duration_seconds
                quality_speed_ratios.append({
                    "strategy": r.strategy_name,
                    "ratio": ratio,
                    "score": r.best_score,
                    "duration": r.duration_seconds
                })
        
        quality_speed_ratios.sort(key=lambda x: x["ratio"], reverse=True)
        
        return {
            "best_quality": {
                "strategy": best_quality.strategy_name,
                "score": best_quality.best_score,
                "duration": best_quality.duration_seconds
            },
            "fastest": {
                "strategy": fastest.strategy_name,
                "score": fastest.best_score,
                "duration": fastest.duration_seconds
            },
            "most_efficient": {
                "strategy": most_efficient.strategy_name,
                "evaluations": most_efficient.total_evaluations,
                "score": most_efficient.best_score
            },
            "quality_speed_ranking": quality_speed_ratios,
            "score_range": {
                "min": min(r.best_score for r in successful_results),
                "max": max(r.best_score for r in successful_results),
                "avg": sum(r.best_score for r in successful_results) / len(successful_results)
            },
            "duration_range": {
                "min": min(r.duration_seconds for r in successful_results),
                "max": max(r.duration_seconds for r in successful_results),
                "avg": sum(r.duration_seconds for r in successful_results) / len(successful_results)
            }
        }
    
    def _generate_recommendation(
        self,
        results: List[StrategyResult],
        requirements: StrategyRequirements
    ) -> Dict[str, Any]:
        """
        Generate strategy recommendation based on requirements.
        
        Args:
            results: Strategy results
            requirements: User requirements
            
        Returns:
            Recommendation with rationale
        """
        successful_results = [r for r in results if r.status == "completed"]
        
        if not successful_results:
            return {
                "recommended_strategy": None,
                "rationale": "No strategies completed successfully",
                "confidence": 0.0
            }
        
        # Filter by hard constraints
        candidates = successful_results
        
        if requirements.max_duration_seconds:
            candidates = [
                r for r in candidates
                if r.duration_seconds <= requirements.max_duration_seconds
            ]
        
        if requirements.min_quality_score:
            candidates = [
                r for r in candidates
                if r.best_score >= requirements.min_quality_score
            ]
        
        if requirements.max_evaluations:
            candidates = [
                r for r in candidates
                if r.total_evaluations <= requirements.max_evaluations
            ]
        
        if not candidates:
            candidates = successful_results  # Fallback to all successful
        
        # Select based on priority
        if requirements.priority == "speed":
            recommended = min(candidates, key=lambda r: r.duration_seconds)
            rationale = f"Fastest execution ({recommended.duration_seconds:.1f}s)"
        
        elif requirements.priority == "quality":
            recommended = max(candidates, key=lambda r: r.best_score)
            rationale = f"Highest quality score ({recommended.best_score:.3f})"
        
        elif requirements.priority == "robustness":
            # Prefer genetic algorithm for robustness
            ga_results = [r for r in candidates if r.strategy_name == "genetic_algorithm"]
            if ga_results:
                recommended = max(ga_results, key=lambda r: r.best_score)
                rationale = f"Population-based diversity ({recommended.best_score:.3f} score)"
            else:
                recommended = max(candidates, key=lambda r: r.best_score)
                rationale = f"Best available quality ({recommended.best_score:.3f})"
        
        else:  # balanced
            # Quality/speed trade-off
            scores = []
            for r in candidates:
                quality_score = r.best_score
                speed_score = 1.0 / (r.duration_seconds / 10.0 + 1.0)  # Normalize
                efficiency_score = r.best_score / (r.total_evaluations / 10.0 + 1.0)
                
                combined_score = (quality_score * 0.4 + speed_score * 0.3 + efficiency_score * 0.3)
                scores.append((r, combined_score))
            
            recommended, score = max(scores, key=lambda x: x[1])
            rationale = f"Best quality/speed/efficiency balance (score: {score:.3f})"
        
        # Calculate confidence
        score_diff = recommended.best_score - min(c.best_score for c in candidates)
        confidence = min(0.95, 0.5 + score_diff)
        
        return {
            "recommended_strategy": recommended.strategy_name,
            "rationale": rationale,
            "confidence": round(confidence, 2),
            "alternatives": [
                {
                    "strategy": r.strategy_name,
                    "score": r.best_score,
                    "duration": r.duration_seconds,
                    "evaluations": r.total_evaluations
                }
                for r in candidates if r.strategy_name != recommended.strategy_name
            ]
        }
    
    def _result_to_dict(self, result: StrategyResult) -> Dict[str, Any]:
        """Convert StrategyResult to dictionary."""
        return {
            "strategy": result.strategy_name,
            "run_id": result.run_id,
            "status": result.status,
            "best_score": result.best_score,
            "best_prompt": result.best_prompt,
            "duration_seconds": result.duration_seconds,
            "iterations_completed": result.iterations_completed,
            "total_evaluations": result.total_evaluations,
            "evaluations_per_iteration": (
                result.total_evaluations / result.iterations_completed
                if result.iterations_completed > 0 else 0
            ),
            "score_per_second": (
                result.best_score / result.duration_seconds
                if result.duration_seconds > 0 else 0
            ),
            "metadata": result.metadata,
            "error": result.error
        }
