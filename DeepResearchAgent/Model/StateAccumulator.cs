namespace DeepResearchAgent.Models;

/// <summary>
/// Represents an accumulator pattern for merging state lists, similar to Python's 
/// @add_messages and operator.add patterns in LangGraph.
/// 
/// This is a key part of the hierarchical state management system that allows
/// multiple sources to contribute to shared lists (raw_notes, messages, etc.)
/// without losing previous data.
/// </summary>
/// <typeparam name="T">The type of items to accumulate</typeparam>
public class StateAccumulator<T> where T : notnull
{
    private readonly List<T> _items;
    private readonly object _lock = new();

    public StateAccumulator()
    {
        _items = new List<T>();
    }

    public StateAccumulator(IEnumerable<T> initialItems)
    {
        _items = new List<T>(initialItems);
    }

    /// <summary>
    /// Get a read-only view of accumulated items.
    /// </summary>
    public IReadOnlyList<T> Items
    {
        get
        {
            lock (_lock)
            {
                return _items.AsReadOnly();
            }
        }
    }

    /// <summary>
    /// Add a single item to the accumulator.
    /// </summary>
    public void Add(T item)
    {
        lock (_lock)
        {
            _items.Add(item);
        }
    }

    /// <summary>
    /// Add multiple items to the accumulator.
    /// </summary>
    public void AddRange(IEnumerable<T> items)
    {
        lock (_lock)
        {
            _items.AddRange(items);
        }
    }

    /// <summary>
    /// Replace all items with new items (useful for state updates).
    /// </summary>
    public void Replace(IEnumerable<T> items)
    {
        lock (_lock)
        {
            _items.Clear();
            _items.AddRange(items);
        }
    }

    /// <summary>
    /// Clear all accumulated items.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _items.Clear();
        }
    }

    /// <summary>
    /// Get the count of accumulated items.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _items.Count;
            }
        }
    }

    /// <summary>
    /// Check if accumulator contains any items.
    /// </summary>
    public bool Any
    {
        get
        {
            lock (_lock)
            {
                return _items.Count > 0;
            }
        }
    }

    /// <summary>
    /// Get the last item, or null if empty.
    /// </summary>
    public T? LastOrDefault
    {
        get
        {
            lock (_lock)
            {
                return _items.Count > 0 ? _items[_items.Count - 1] : default;
            }
        }
    }

    /// <summary>
    /// Create a shallow copy of this accumulator.
    /// </summary>
    public StateAccumulator<T> Clone()
    {
        lock (_lock)
        {
            return new StateAccumulator<T>(_items);
        }
    }

    /// <summary>
    /// Merge two accumulators into a new one (union operation).
    /// </summary>
    public static StateAccumulator<T> operator +(StateAccumulator<T> left, StateAccumulator<T> right)
    {
        var result = new StateAccumulator<T>(left.Items);
        result.AddRange(right.Items);
        return result;
    }
}
