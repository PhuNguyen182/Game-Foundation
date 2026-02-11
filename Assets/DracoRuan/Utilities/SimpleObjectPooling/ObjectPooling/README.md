# Object Pooling System

A high-performance, generic object pooling system for C# plain classes in Unity.

## Features

✅ **Generic Support** - Pool any C# class without inheritance  
✅ **Static Access** - Global access via `ObjectPoolManager`  
✅ **SOLID Principles** - Clean, maintainable architecture  
✅ **High Performance** - O(1) get/release with stack-based storage  
✅ **Optional Lifecycle** - Implement `IPoolable` for callbacks  
✅ **Flexible Configuration** - Custom factory functions and capacity limits  

## Quick Start

### Basic Usage

```csharp
using ObjectPooling;

// Define any class
public class Bullet
{
    public float Speed { get; set; }
    public float Damage { get; set; }
}

// Get from pool (pool created automatically)
var bullet = ObjectPoolManager.Get<Bullet>();

// Use the object
bullet.Speed = 10f;

// Return to pool
ObjectPoolManager.Release(bullet);
```

### With Lifecycle Callbacks

```csharp
public class Enemy : IPoolable
{
    public int Health { get; set; }

    public void OnGet()
    {
        // Called when retrieved from pool
        Health = 100;
    }

    public void OnRelease()
    {
        // Called when returned to pool
        Health = 0;
    }
}

var enemy = ObjectPoolManager.Get<Enemy>(); // OnGet() called
ObjectPoolManager.Release(enemy); // OnRelease() called
```

### Custom Configuration

```csharp
// Create pool with custom settings
ObjectPoolManager.CreatePool(new PoolConfig<Particle>
{
    InitialCapacity = 100,  // Pre-allocate 100 objects
    MaxCapacity = 200,      // Max 200 in pool (0 = unlimited)
    CreateFunc = () => new Particle { Size = 1f } // Custom factory
});
```

## API Reference

### ObjectPoolManager (Static)

| Method | Description |
|--------|-------------|
| `Get<T>()` | Get object from pool (creates pool if needed) |
| `Release<T>(T obj)` | Return object to pool |
| `CreatePool<T>(PoolConfig<T>)` | Create pool with custom config |
| `ClearPool<T>()` | Clear all objects from pool |
| `RemovePool<T>()` | Remove pool completely |
| `GetPoolCount<T>()` | Get current pool count |
| `ClearAllPools()` | Clear all pools |
| `RemoveAllPools()` | Remove all pools |

### PoolConfig<T>

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `InitialCapacity` | int | 10 | Pre-allocated pool size |
| `MaxCapacity` | int | 0 | Max pool size (0 = unlimited) |
| `CreateFunc` | Func<T> | null | Custom factory function |

### IPoolable (Optional Interface)

| Method | Description |
|--------|-------------|
| `OnGet()` | Called when object retrieved from pool |
| `OnRelease()` | Called when object returned to pool |

## Architecture

### SOLID Principles

- **Single Responsibility**: Each class has one clear purpose
  - `IPoolable`: Lifecycle contract
  - `ObjectPool<T>`: Manages single type pool
  - `ObjectPoolManager`: Coordinates multiple pools
  - `PoolConfig<T>`: Configuration data

- **Open/Closed**: Extensible via factory functions and `IPoolable`

- **Liskov Substitution**: Generic constraints ensure type safety

- **Interface Segregation**: `IPoolable` is optional and minimal

- **Dependency Inversion**: Depends on abstractions (`Func<T>`, `IPoolable`)

### Performance Optimizations

- **Stack-based storage** for O(1) get/release operations
- **Pre-allocation** of initial capacity to reduce runtime allocations
- **Dictionary lookup** for fast pool retrieval by type
- **Lazy initialization** - pools created only when needed
- **Optional capacity limits** to prevent memory bloat

## Files

- **IPoolable.cs** - Optional lifecycle interface
- **PoolConfig.cs** - Configuration class
- **ObjectPool.cs** - Generic pool implementation
- **ObjectPoolManager.cs** - Static manager for global access
- **UsageExamples.cs** - Comprehensive usage examples
- **README.md** - This documentation

## Use Cases

- Bullet/projectile pooling in games
- Enemy/NPC spawning systems
- Particle effect systems
- Network message objects
- Database connection pooling
- Any frequently created/destroyed objects

## Notes

- Objects must be **classes** (reference types)
- Default factory uses `Activator.CreateInstance<T>()` (requires parameterless constructor)
- For classes without parameterless constructors, provide custom `CreateFunc`
- `IPoolable` interface is completely optional
- Thread-safety: Not thread-safe by default (add locking if needed for multi-threaded scenarios)
