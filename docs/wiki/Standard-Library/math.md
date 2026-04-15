# math — Mathematics

Maths functions and constants. Core module — auto-available, no import required.

## Constants

| Constant | Value |
|----------|-------|
| `math.pi` | 3.14159... |
| `math.e` | 2.71828... |
| `math.tau` | 6.28318... |

## Functions

| Function | Signature | Description |
|----------|-----------|-------------|
| `math.sqrt(n: float)` | `→ float` | Throws if n < 0 |
| `math.pow(base: float, exp: float)` | `→ float` | |
| `math.log(n: float)` | `→ float` | Natural log; throws if n ≤ 0 |
| `math.log10(n: float)` | `→ float` | |
| `math.sin(n: float)` | `→ float` | Radians |
| `math.cos(n: float)` | `→ float` | |
| `math.tan(n: float)` | `→ float` | |
| `math.asin(n: float)` | `→ float` | |
| `math.acos(n: float)` | `→ float` | |
| `math.atan(n: float)` | `→ float` | |
| `math.atan2(y, x: float)` | `→ float` | |
| `math.toRadians(degrees: float)` | `→ float` | |
| `math.toDegrees(radians: float)` | `→ float` | |
| `math.random()` | `→ float` | [0.0, 1.0) uniform |
| `math.randomInt(min, max: int)` | `→ int` | Inclusive both ends |
| `math.randomSeed(seed: int)` | `→ void` | Deterministic testing |

`abs`, `floor`, `ceil`, `round`, `clamp`, `min`, `max` live on the type
registry as instance or static methods. No overlap with `math` module.

See also: [int type](../Type-Registry/int.md), [float type](../Type-Registry/float.md)
