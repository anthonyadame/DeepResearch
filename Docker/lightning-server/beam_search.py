# Beam Search Optimization Strategy for APO
# Implements parallel exploration with beam pruning for prompt optimization

import logging
from typing import List, Dict, Any, Optional, Tuple
from datetime import datetime
import asyncio

logger = logging.getLogger(__name__)


class BeamSearchStrategy:
    """
    Beam Search optimization strategy for prompt refinement.
    
    Unlike iterative refinement which explores one path, beam search maintains
    multiple candidate prompts (beam) and explores them in parallel, pruning
    low-scoring candidates at each step.
    
    Algorithm:
    1. Start with initial prompt as the beam (size 1)
    2. For each iteration:
       a. Generate variations of each prompt in the beam
       b. Evaluate all variations in parallel
       c. Keep top-k (beam_width) best scoring prompts
       d. Continue with pruned beam
    3. Return best prompt from final beam
    
    Benefits:
    - Explores multiple paths simultaneously
    - Avoids local optima through diversity
    - Balances exploration vs exploitation
    
    Trade-offs:
    - Higher computational cost (beam_width * variations per iteration)
    - Needs more evaluations than iterative refinement
    - Best for cases where diversity is valuable
    """
    
    def __init__(
        self,
        beam_width: int = 3,
        variations_per_prompt: int = 2,
        max_concurrent_evaluations: int = 10
    ):
        """
        Initialize beam search strategy.
        
        Args:
            beam_width: Number of candidates to keep at each step (default: 3)
            variations_per_prompt: How many variations to generate per candidate (default: 2)
            max_concurrent_evaluations: Limit concurrent async evaluations (default: 10)
        """
        self.beam_width = beam_width
        self.variations_per_prompt = variations_per_prompt
        self.max_concurrent_evaluations = max_concurrent_evaluations
        
        logger.info(
            f"🔍 Initialized BeamSearchStrategy: "
            f"beam_width={beam_width}, "
            f"variations_per_prompt={variations_per_prompt}"
        )
    
    def generate_variations(self, prompt: str, count: int = 2) -> List[str]:
        """
        Generate variations of a prompt for beam expansion.
        
        Strategies:
        1. Add specificity: "You are an expert" → "You are an expert with 10+ years"
        2. Add constraints: "Write code" → "Write clean, well-documented code"
        3. Add structure: "Answer questions" → "Answer questions in 3 steps: 1) Analyze 2) Explain 3) Example"
        4. Rephrase: Maintain intent, change wording
        
        Args:
            prompt: Base prompt to generate variations from
            count: Number of variations to generate
        
        Returns:
            List of prompt variations
        """
        variations = []
        
        # Variation 1: Add specificity/expertise
        if count >= 1:
            var1 = self._add_expertise(prompt)
            variations.append(var1)
        
        # Variation 2: Add structure/format
        if count >= 2:
            var2 = self._add_structure(prompt)
            variations.append(var2)
        
        # Variation 3: Add constraints/quality guidance
        if count >= 3:
            var3 = self._add_constraints(prompt)
            variations.append(var3)
        
        # Variation 4: Simplify (remove redundancy)
        if count >= 4:
            var4 = self._simplify(prompt)
            variations.append(var4)
        
        return variations[:count]
    
    def _add_expertise(self, prompt: str) -> str:
        """Add expertise or experience level to prompt."""
        # Check if already has expertise markers
        expertise_markers = ["expert", "experienced", "professional", "skilled"]
        has_expertise = any(marker in prompt.lower() for marker in expertise_markers)
        
        if not has_expertise:
            # Add at the beginning if starts with "You are"
            if prompt.strip().lower().startswith("you are"):
                return prompt.replace("You are", "You are an expert", 1)
            else:
                return f"As an expert, {prompt}"
        else:
            # Add years of experience
            return f"{prompt}\n\nLeverage your extensive experience to provide high-quality responses."
    
    def _add_structure(self, prompt: str) -> str:
        """Add structured output guidance to prompt."""
        # Check if already has structure
        structure_markers = ["step", "format", "structure", "organize"]
        has_structure = any(marker in prompt.lower() for marker in structure_markers)
        
        if not has_structure:
            return f"{prompt}\n\nStructure your responses clearly with logical organization."
        else:
            return f"{prompt}\n\nEnsure each response follows a consistent, well-organized format."
    
    def _add_constraints(self, prompt: str) -> str:
        """Add quality constraints and guidance."""
        # Check for existing quality markers
        quality_markers = ["clear", "accurate", "helpful", "concise"]
        has_quality = any(marker in prompt.lower() for marker in quality_markers)
        
        if not has_quality:
            return f"{prompt}\n\nProvide clear, accurate, and helpful responses."
        else:
            return f"{prompt}\n\nEnsure responses are both comprehensive and concise, balancing detail with brevity."
    
    def _simplify(self, prompt: str) -> str:
        """Simplify prompt by removing potential redundancy."""
        lines = prompt.split('\n')
        # Keep unique non-empty lines (simple deduplication)
        unique_lines = []
        seen = set()
        for line in lines:
            normalized = line.strip().lower()
            if normalized and normalized not in seen:
                unique_lines.append(line)
                seen.add(normalized)
        
        simplified = '\n'.join(unique_lines)
        
        # If we removed something, return simplified version
        if len(simplified) < len(prompt) * 0.9:
            return simplified
        else:
            # If not much removed, add a conciseness directive
            return f"{prompt}\n\nBe concise while maintaining clarity."
    
    async def run_beam_search(
        self,
        initial_prompt: str,
        iterations: int,
        evaluator_func: Any,  # async function(prompt) -> {"score": float, ...}
        model: str,
        criteria: List[str]
    ) -> Dict[str, Any]:
        """
        Execute beam search optimization.
        
        Args:
            initial_prompt: Starting prompt
            iterations: Number of search iterations
            evaluator_func: Async function to evaluate prompts
            model: Model name for evaluation
            criteria: Evaluation criteria
        
        Returns:
            Dict with best_prompt, best_score, all_iterations, beam_history
        """
        logger.info(
            f"🚀 Starting beam search optimization: "
            f"{iterations} iterations, beam_width={self.beam_width}"
        )
        
        # Initialize beam with the initial prompt
        beam: List[Tuple[str, float]] = [(initial_prompt, 0.0)]  # (prompt, score)
        all_iterations = []
        beam_history = []
        
        start_time = datetime.utcnow()
        
        for iteration in range(iterations):
            iter_start = datetime.utcnow()
            logger.info(f"  Iteration {iteration + 1}/{iterations}: Beam size = {len(beam)}")
            
            # Generate variations for each prompt in the beam
            candidates = []
            for prompt, prev_score in beam:
                variations = self.generate_variations(prompt, self.variations_per_prompt)
                for variation in variations:
                    candidates.append(variation)
            
            logger.info(f"    Generated {len(candidates)} candidate variations")
            
            # Evaluate all candidates in parallel (with concurrency limit)
            evaluated_candidates = await self._evaluate_candidates_parallel(
                candidates,
                evaluator_func,
                model,
                criteria
            )
            
            # Sort by score and keep top beam_width
            evaluated_candidates.sort(key=lambda x: x[1], reverse=True)
            beam = evaluated_candidates[:self.beam_width]
            
            # Record iteration results
            iter_duration = (datetime.utcnow() - iter_start).total_seconds()
            iteration_result = {
                "iteration": iteration + 1,
                "candidates_evaluated": len(evaluated_candidates),
                "beam_size": len(beam),
                "best_score": beam[0][1] if beam else 0.0,
                "best_prompt": beam[0][0] if beam else "",
                "beam_scores": [score for _, score in beam],
                "duration_seconds": iter_duration
            }
            all_iterations.append(iteration_result)
            beam_history.append([(prompt, score) for prompt, score in beam])
            
            logger.info(
                f"    Best score: {beam[0][1]:.3f}, "
                f"Beam: {[f'{s:.3f}' for _, s in beam]}, "
                f"Duration: {iter_duration:.2f}s"
            )
        
        total_duration = (datetime.utcnow() - start_time).total_seconds()
        
        # Return best prompt from final beam
        best_prompt, best_score = beam[0] if beam else (initial_prompt, 0.0)
        
        logger.info(
            f"✅ Beam search complete: "
            f"Best score = {best_score:.3f}, "
            f"Total duration = {total_duration:.2f}s"
        )
        
        return {
            "best_prompt": best_prompt,
            "best_score": best_score,
            "improvement": best_score - 0.0,  # Assuming initial score is 0
            "iterations": all_iterations,
            "beam_history": beam_history,
            "total_duration_seconds": total_duration,
            "strategy": "beam_search",
            "beam_width": self.beam_width,
            "variations_per_prompt": self.variations_per_prompt
        }
    
    async def _evaluate_candidates_parallel(
        self,
        candidates: List[str],
        evaluator_func: Any,
        model: str,
        criteria: List[str]
    ) -> List[Tuple[str, float]]:
        """
        Evaluate multiple candidates in parallel with concurrency limit.
        
        Args:
            candidates: List of prompts to evaluate
            evaluator_func: Async evaluation function
            model: Model name
            criteria: Evaluation criteria
        
        Returns:
            List of (prompt, score) tuples
        """
        # Create semaphore for concurrency control
        semaphore = asyncio.Semaphore(self.max_concurrent_evaluations)
        
        async def evaluate_with_semaphore(prompt: str) -> Tuple[str, float]:
            async with semaphore:
                try:
                    result = await evaluator_func(
                        prompt=prompt,
                        model=model,
                        criteria=criteria
                    )
                    score = result.get("score", 0.0)
                    return (prompt, score)
                except Exception as e:
                    logger.warning(f"Evaluation failed for candidate: {e}")
                    return (prompt, 0.0)
        
        # Evaluate all candidates in parallel
        tasks = [evaluate_with_semaphore(candidate) for candidate in candidates]
        results = await asyncio.gather(*tasks)
        
        return results


# Utility function to create beam search strategy from config
def create_beam_search_strategy(config: Any) -> BeamSearchStrategy:
    """
    Create BeamSearchStrategy from configuration.
    
    Args:
        config: Configuration object with beam search settings
    
    Returns:
        Configured BeamSearchStrategy instance
    """
    beam_width = getattr(config, 'beam_width', 3)
    variations_per_prompt = getattr(config, 'variations_per_prompt', 2)
    max_concurrent = getattr(config, 'max_concurrent_evaluations', 10)
    
    return BeamSearchStrategy(
        beam_width=beam_width,
        variations_per_prompt=variations_per_prompt,
        max_concurrent_evaluations=max_concurrent
    )
