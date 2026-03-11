"""
Genetic Algorithm Strategy for APO (Agent Prompt Optimization)

Population-based optimization using evolutionary principles:
- Population: Set of candidate prompts
- Crossover: Combine best prompts to create offspring
- Mutation: Random variations to maintain diversity
- Selection: Fitness-based selection of next generation

Configuration:
- population_size: Number of candidates per generation (default: 5)
- mutation_rate: Probability of mutation (default: 0.3)
- crossover_rate: Probability of crossover (default: 0.7)
- tournament_size: Size of tournament selection (default: 3)
"""

import logging
import random
import asyncio
from typing import List, Tuple, Callable, Dict, Any, Optional
from datetime import datetime

logger = logging.getLogger(__name__)


class GeneticAlgorithmStrategy:
    """
    Genetic algorithm for prompt optimization.
    
    Uses evolutionary principles:
    1. Initialize population from initial prompt
    2. Evaluate fitness of all candidates
    3. Select parents using tournament selection
    4. Create offspring through crossover and mutation
    5. Replace worst individuals with offspring
    6. Repeat for specified generations
    """
    
    def __init__(
        self,
        population_size: int = 5,
        mutation_rate: float = 0.3,
        crossover_rate: float = 0.7,
        tournament_size: int = 3,
        max_concurrent_evaluations: int = 10
    ):
        """
        Initialize genetic algorithm strategy.
        
        Args:
            population_size: Number of individuals in population
            mutation_rate: Probability of mutation (0.0-1.0)
            crossover_rate: Probability of crossover (0.0-1.0)
            tournament_size: Number of candidates in tournament selection
            max_concurrent_evaluations: Max parallel evaluations
        """
        self.population_size = population_size
        self.mutation_rate = mutation_rate
        self.crossover_rate = crossover_rate
        self.tournament_size = min(tournament_size, population_size)
        self.max_concurrent_evaluations = max_concurrent_evaluations
        
        logger.info(
            f"GeneticAlgorithmStrategy initialized: "
            f"pop_size={population_size}, mutation={mutation_rate}, "
            f"crossover={crossover_rate}, tournament={tournament_size}"
        )
    
    def initialize_population(self, initial_prompt: str, size: int) -> List[str]:
        """
        Create initial population from base prompt.
        
        Strategies:
        1. Original prompt (elite)
        2. Add task-specific guidance
        3. Add formatting instructions
        4. Add quality constraints
        5. Simplify and clarify
        
        Args:
            initial_prompt: Base prompt to evolve from
            size: Population size
            
        Returns:
            List of prompt variations
        """
        population = [initial_prompt]  # Keep original as elite
        
        # Generate diverse variations
        variations = [
            self._add_task_guidance(initial_prompt),
            self._add_formatting_guidance(initial_prompt),
            self._add_quality_constraints(initial_prompt),
            self._simplify_prompt(initial_prompt),
        ]
        
        # Add variations until we reach population size
        for i in range(size - 1):
            if i < len(variations):
                population.append(variations[i])
            else:
                # Create additional variations by combining strategies
                base_idx = i % len(variations)
                variation = variations[base_idx]
                # Apply random mutation for diversity
                if random.random() < 0.5:
                    variation = self._mutate_prompt(variation)
                population.append(variation)
        
        logger.info(f"Initialized population of {len(population)} prompts")
        return population
    
    def _add_task_guidance(self, prompt: str) -> str:
        """Add task-specific guidance to prompt."""
        if "Task:" not in prompt and "task" not in prompt.lower():
            guidance = "\n\nTask: Provide clear, accurate, and helpful responses. Focus on understanding the user's intent and delivering value."
            return prompt + guidance
        return prompt + "\n\n(Enhanced with task clarity)"
    
    def _add_formatting_guidance(self, prompt: str) -> str:
        """Add formatting instructions to prompt."""
        if "format" not in prompt.lower():
            formatting = "\n\nFormatting: Structure your responses with clear sections, use bullet points for lists, and provide examples where helpful."
            return prompt + formatting
        return prompt + "\n\n(Enhanced with formatting guidance)"
    
    def _add_quality_constraints(self, prompt: str) -> str:
        """Add quality constraints to prompt."""
        if "quality" not in prompt.lower() and "ensure" not in prompt.lower():
            constraints = "\n\nQuality requirements: Ensure accuracy, completeness, and clarity. Verify claims and provide sources when appropriate."
            return prompt + constraints
        return prompt + "\n\n(Enhanced with quality constraints)"
    
    def _simplify_prompt(self, prompt: str) -> str:
        """Simplify prompt by removing redundancy."""
        # Remove duplicate sentences
        sentences = prompt.split('. ')
        unique_sentences = []
        seen = set()
        
        for sentence in sentences:
            sentence_lower = sentence.lower().strip()
            if sentence_lower and sentence_lower not in seen:
                unique_sentences.append(sentence)
                seen.add(sentence_lower)
        
        simplified = '. '.join(unique_sentences)
        if not simplified.endswith('.'):
            simplified += '.'
        
        return simplified
    
    def _mutate_prompt(self, prompt: str) -> str:
        """
        Apply random mutation to prompt.
        
        Mutation strategies:
        1. Add emphasis words
        2. Add clarifying phrase
        3. Add example request
        4. Modify tone
        
        Args:
            prompt: Prompt to mutate
            
        Returns:
            Mutated prompt
        """
        mutation_type = random.choice(['emphasis', 'clarify', 'example', 'tone'])
        
        if mutation_type == 'emphasis':
            # Add emphasis words
            emphasis = random.choice([
                "Remember to be thorough and precise.",
                "Pay special attention to detail.",
                "Focus on delivering high-quality results.",
                "Prioritize accuracy and completeness."
            ])
            return prompt + "\n\n" + emphasis
        
        elif mutation_type == 'clarify':
            # Add clarifying phrase
            clarification = random.choice([
                "To clarify: provide comprehensive responses that address all aspects of the request.",
                "In other words: ensure your responses are clear, accurate, and helpful.",
                "Specifically: focus on understanding the context and providing relevant information."
            ])
            return prompt + "\n\n" + clarification
        
        elif mutation_type == 'example':
            # Request examples
            example_request = random.choice([
                "Include examples when appropriate to illustrate your points.",
                "Provide concrete examples to make your responses more actionable.",
                "Use real-world examples to clarify complex concepts."
            ])
            return prompt + "\n\n" + example_request
        
        else:  # tone
            # Modify tone
            tone_modifier = random.choice([
                "Maintain a professional and helpful tone throughout your responses.",
                "Be conversational yet informative in your communication style.",
                "Use clear and accessible language that's easy to understand."
            ])
            return prompt + "\n\n" + tone_modifier
    
    def _crossover_prompts(self, parent1: str, parent2: str) -> Tuple[str, str]:
        """
        Combine two parent prompts to create offspring.
        
        Uses sentence-level crossover:
        1. Split both parents into sentences
        2. Randomly select crossover point
        3. Combine first part of parent1 with second part of parent2
        4. Combine first part of parent2 with second part of parent1
        
        Args:
            parent1: First parent prompt
            parent2: Second parent prompt
            
        Returns:
            Tuple of two offspring prompts
        """
        # Split into sentences
        sentences1 = [s.strip() + '.' for s in parent1.split('.') if s.strip()]
        sentences2 = [s.strip() + '.' for s in parent2.split('.') if s.strip()]
        
        if len(sentences1) < 2 or len(sentences2) < 2:
            # If prompts are too short, return parents
            return parent1, parent2
        
        # Select crossover points
        point1 = random.randint(1, len(sentences1) - 1)
        point2 = random.randint(1, len(sentences2) - 1)
        
        # Create offspring
        offspring1 = ' '.join(sentences1[:point1] + sentences2[point2:])
        offspring2 = ' '.join(sentences2[:point2] + sentences1[point1:])
        
        return offspring1, offspring2
    
    def _tournament_selection(
        self,
        population: List[Tuple[str, float]],
        tournament_size: int
    ) -> str:
        """
        Select individual using tournament selection.
        
        Args:
            population: List of (prompt, fitness) tuples
            tournament_size: Number of competitors in tournament
            
        Returns:
            Selected prompt
        """
        # Randomly select tournament competitors
        competitors = random.sample(population, min(tournament_size, len(population)))
        
        # Return best competitor
        winner = max(competitors, key=lambda x: x[1])
        return winner[0]
    
    async def _evaluate_population_parallel(
        self,
        population: List[str],
        evaluator_func: Callable,
        model: str,
        criteria: List[str]
    ) -> List[Tuple[str, float]]:
        """
        Evaluate all individuals in population in parallel.
        
        Args:
            population: List of prompts to evaluate
            evaluator_func: Async function to evaluate prompts
            model: Model to use for evaluation
            criteria: Evaluation criteria
            
        Returns:
            List of (prompt, score) tuples
        """
        semaphore = asyncio.Semaphore(self.max_concurrent_evaluations)
        
        async def evaluate_with_limit(prompt: str) -> Tuple[str, float]:
            async with semaphore:
                try:
                    result = await evaluator_func(prompt, model, criteria)
                    score = result.get("score", 0.0) if isinstance(result, dict) else result
                    return (prompt, score)
                except Exception as e:
                    logger.error(f"Evaluation failed for prompt: {e}")
                    return (prompt, 0.0)
        
        # Evaluate all prompts in parallel
        results = await asyncio.gather(*[
            evaluate_with_limit(prompt) for prompt in population
        ])
        
        return results
    
    async def run_genetic_algorithm(
        self,
        initial_prompt: str,
        generations: int,
        evaluator_func: Callable,
        model: str,
        criteria: List[str]
    ) -> Dict[str, Any]:
        """
        Run genetic algorithm optimization.
        
        Args:
            initial_prompt: Starting prompt
            generations: Number of generations to evolve
            evaluator_func: Async function to evaluate prompts
            model: Model to use for evaluation
            criteria: Evaluation criteria
            
        Returns:
            Dictionary with:
                - best_prompt: Best prompt found
                - best_score: Score of best prompt
                - generations: Generation history
                - total_duration: Total execution time
        """
        start_time = datetime.now()
        logger.info(
            f"🧬 Starting genetic algorithm: {generations} generations, "
            f"population={self.population_size}"
        )
        
        # Initialize population
        population_prompts = self.initialize_population(initial_prompt, self.population_size)
        
        # Evaluate initial population
        population = await self._evaluate_population_parallel(
            population_prompts, evaluator_func, model, criteria
        )
        population.sort(key=lambda x: x[1], reverse=True)
        
        generation_history = []
        best_overall_prompt = population[0][0]
        best_overall_score = population[0][1]
        
        logger.info(
            f"Initial population evaluated: best_score={best_overall_score:.3f}, "
            f"avg_score={sum(s for _, s in population) / len(population):.3f}"
        )
        
        # Evolution loop
        for gen in range(generations):
            gen_start = datetime.now()
            
            # Create next generation
            offspring = []
            
            # Elitism: keep best individual
            offspring.append(population[0][0])
            
            # Generate offspring through crossover and mutation
            while len(offspring) < self.population_size:
                # Select parents
                parent1 = self._tournament_selection(population, self.tournament_size)
                parent2 = self._tournament_selection(population, self.tournament_size)
                
                # Crossover
                if random.random() < self.crossover_rate:
                    child1, child2 = self._crossover_prompts(parent1, parent2)
                else:
                    child1, child2 = parent1, parent2
                
                # Mutation
                if random.random() < self.mutation_rate:
                    child1 = self._mutate_prompt(child1)
                if random.random() < self.mutation_rate:
                    child2 = self._mutate_prompt(child2)
                
                offspring.extend([child1, child2])
            
            # Trim to population size
            offspring = offspring[:self.population_size]
            
            # Evaluate new generation
            population = await self._evaluate_population_parallel(
                offspring, evaluator_func, model, criteria
            )
            population.sort(key=lambda x: x[1], reverse=True)
            
            # Track best overall
            gen_best_score = population[0][1]
            if gen_best_score > best_overall_score:
                best_overall_score = gen_best_score
                best_overall_prompt = population[0][0]
            
            # Record generation metrics
            gen_duration = (datetime.now() - gen_start).total_seconds()
            avg_score = sum(s for _, s in population) / len(population)
            
            generation_history.append({
                "generation": gen + 1,
                "best_score": gen_best_score,
                "avg_score": avg_score,
                "population_diversity": len(set(p for p, _ in population)),
                "duration_seconds": gen_duration
            })
            
            logger.info(
                f"Generation {gen + 1}/{generations}: "
                f"best={gen_best_score:.3f}, avg={avg_score:.3f}, "
                f"diversity={len(set(p for p, _ in population))}/{self.population_size}, "
                f"duration={gen_duration:.1f}s"
            )
        
        total_duration = (datetime.now() - start_time).total_seconds()
        
        logger.info(
            f"✅ Genetic algorithm completed: best_score={best_overall_score:.3f}, "
            f"total_duration={total_duration:.1f}s"
        )
        
        return {
            "best_prompt": best_overall_prompt,
            "best_score": best_overall_score,
            "generations": generation_history,
            "total_duration": total_duration,
            "final_population": population,
            "config": {
                "population_size": self.population_size,
                "mutation_rate": self.mutation_rate,
                "crossover_rate": self.crossover_rate,
                "tournament_size": self.tournament_size
            }
        }


def create_genetic_algorithm_strategy(config: Any) -> GeneticAlgorithmStrategy:
    """
    Create genetic algorithm strategy from configuration.
    
    Args:
        config: Configuration object with GA parameters
        
    Returns:
        GeneticAlgorithmStrategy instance
    """
    population_size = getattr(config, 'population_size', 5)
    mutation_rate = getattr(config, 'mutation_rate', 0.3)
    crossover_rate = getattr(config, 'crossover_rate', 0.7)
    tournament_size = getattr(config, 'tournament_size', 3)
    
    return GeneticAlgorithmStrategy(
        population_size=population_size,
        mutation_rate=mutation_rate,
        crossover_rate=crossover_rate,
        tournament_size=tournament_size
    )
