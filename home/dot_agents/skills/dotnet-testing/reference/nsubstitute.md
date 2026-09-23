# NSubstitute

NSubstitute is the repo-default mocking library. Substitute **interfaces and abstract classes** — never concrete classes (banned in SKILL.md). For automatic substitution of constructor dependencies, see `AutoFixture.AutoNSubstitute` in [autofixture.md](autofixture.md).

```csharp
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using AwesomeAssertions;
using Xunit;
```

Three-part naming, 3A, and FIRST all apply — see [reference/fundamentals.md](fundamentals.md) and [reference/naming.md](naming.md).

## The five test-double roles, in one substitute API

Per Meszaros (*xUnit Test Patterns*), these are roles, not types — the same `Substitute.For<T>()` plays whichever role the test needs.

| Role  | Purpose                            | NSubstitute idiom                            |
| ----- | ---------------------------------- | -------------------------------------------- |
| Dummy | Satisfies a parameter; not used    | `Substitute.For<T>()` with no setup          |
| Stub  | Returns canned data for a scenario | `.Returns(value)`                            |
| Fake  | Lightweight working implementation | Hand-rolled class implementing the interface |
| Spy   | Records calls for later inspection | `.Received()`                                |
| Mock  | Enforces expected interactions     | `.Received(n)` strict verification           |

`Stub` answers "what does the SUT *read*?". `Mock` answers "what does the SUT *write*?". A single substitute often plays both roles in one test — that's fine; the role labels are about which assertions you actually make, not how the object was constructed. Worked code for each role lives in [templates/nsubstitute/mock-patterns.cs](../templates/nsubstitute/mock-patterns.cs).

## Creating substitutes

```csharp
var repo        = Substitute.For<IUserRepository>();   // interface (preferred)
var baseService = Substitute.For<BaseService>();        // abstract / virtual members
var multi       = Substitute.For<IService, IDisposable>(); // multi-interface
```

## Configuring return values

```csharp
// Exact argument
_repository.GetById(1).Returns(new User { Id = 1, Name = "John" });

// Any argument
_service.Process(Arg.Any<string>()).Returns("processed");

// Sequence — consecutive calls receive 1, 2, 3, 4, 5
_generator.GetNext().Returns(1, 2, 3, 4, 5);

// Computed from call args
_calculator.Add(Arg.Any<int>(), Arg.Any<int>())
           .Returns(ci => (int)ci[0] + (int)ci[1]);

// Conditional matching
_service.Process(Arg.Is<string>(x => x.StartsWith("test")))
        .Returns("test-result");
```

`ReturnsForAnyArgs` ignores any argument matchers that were configured and replaces them with "any":

```csharp
_service.Process("specific").ReturnsForAnyArgs("default");
_service.Process("anything").Should().Be("default");
```

## Throwing exceptions

```csharp
using NSubstitute.ExceptionExtensions;

_service.RiskyOperation()
        .Throws(new InvalidOperationException("Something went wrong"));

_service.RiskyOperationAsync()
        .Throws(new InvalidOperationException("Async operation failed"));
```

## Argument matchers

```csharp
_service.Process(Arg.Any<string>()).Returns("ok");
_service.Process(Arg.Is<string>(x => x.Length > 5)).Returns("long");

// Capture for inspection
string? captured = null;
_service.Process(Arg.Do<string>(x => captured = x)).Returns("ok");
_service.Process("test");
captured.Should().Be("test");
```

When mixing matchers with concrete values in the same call, **every** argument must be a matcher — NSubstitute throws otherwise.

## Verifying calls

```csharp
_service.Received().Process("test");                          // at least once
_service.Received(2).Process(Arg.Any<string>());              // exactly N
_service.DidNotReceive().Delete(Arg.Any<int>());              // never
_service.ReceivedWithAnyArgs().Process(default!);             // ignore matchers
Received.InOrder(() =>                                        // ordered
{
    _service.Start();
    _service.Process();
    _service.Stop();
});
```

## Capturing arguments for state assertions

`Arg.Do` captures the value at call time, so subsequent mutation of the same object does not corrupt the assertion:

```csharp
[Fact]
public void RegisterUser_WhenRegistering_ShouldHashPassword()
{
    var repository = Substitute.For<IUserRepository>();
    var sut        = new UserService(repository);

    User? captured = null;
    repository.Save(Arg.Do<User>(u => captured = u));

    sut.RegisterUser("john@example.com", "password123");

    captured.Should().NotBeNull();
    captured!.Email.Should().Be("john@example.com");
    captured.PasswordHash.Should().NotBe("password123");
    captured.PasswordHash.Length.Should().BeGreaterThan(20);
}
```

Inline multi-condition verification works when capture isn't needed:

```csharp
repository.Received().Save(Arg.Is<User>(u =>
    u.Id > 0 && u.Email.Contains("@") && u.IsActive));
```

Full set of capture/match/order patterns: [templates/nsubstitute/verification-examples.cs](../templates/nsubstitute/verification-examples.cs).

## Async substitution

```csharp
repository.GetByIdAsync(123)
          .Returns(Task.FromResult(new User { Id = 123, Name = "John" }));

await sut.GetUserAsync(123);

await repository.Received(1).GetByIdAsync(123);   // MUST be awaited
```

Always `await` the verification on async methods — without `await`, the verification never actually runs and a broken expectation passes silently.

For async throw:

```csharp
repository.SaveAsync(Arg.Any<User>())
          .Throws(new InvalidOperationException("Database error"));

Func<Task> act = () => sut.SaveUserAsync(new User { Name = "John" });
await act.Should().ThrowAsync<InvalidOperationException>()
         .WithMessage("Database error");
```

## `out` / `ref` parameters

```csharp
_service.TryGetValue("key", out Arg.Any<string>())
        .Returns(ci => { ci[1] = "value"; return true; });
```

## `When ... Do` for void methods

`When(...).Do(...)` lets you side-effect on a void call (useful for capturing arguments when `Arg.Do` doesn't fit):

```csharp
User? captured = null;
repository.When(x => x.Update(Arg.Any<User>()))
          .Do(ci => captured = ci.Arg<User>());
```

## ILogger verification

`ILogger.LogInformation`/`LogWarning`/etc. are extension methods. NSubstitute can usually verify them directly:

```csharp
_logger.Received(1).LogInformation("User registered: {Email}", "john@example.com");
```

If the analyzer can't bind the extension call (older runtimes, weird overloads), drop down to the underlying `Log`:

```csharp
_logger.Received(1).Log(
    LogLevel.Warning,
    Arg.Any<EventId>(),
    Arg.Is<object>(v => v.ToString()!.Contains("Source file not found")),
    null,
    Arg.Any<Func<object, Exception?, string>>());
```

Prefer asserting on observable state over log strings — log assertions are brittle and banned by the umbrella SKILL.md unless logs are part of the contract.

## Practical pattern: shared SUT in the constructor

```csharp
public class FileBackupServiceTests
{
    private readonly IFileSystem               _fileSystem       = Substitute.For<IFileSystem>();
    private readonly IDateTimeProvider         _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly IBackupRepository         _backupRepository = Substitute.For<IBackupRepository>();
    private readonly ILogger<FileBackupService> _logger           = Substitute.For<ILogger<FileBackupService>>();
    private readonly FileBackupService         _sut;

    public FileBackupServiceTests() =>
        _sut = new FileBackupService(_fileSystem, _dateTimeProvider, _backupRepository, _logger);
}
```

When the same fixture starts repeating across files, extract a thin abstract base class with `protected` substitutes and `Setup*` helpers — but if your `Setup*` methods start branching on parameters, you've reinvented [reference/builder-pattern.md](builder-pattern.md), and that's where the data belongs instead.

## What to substitute, and what not to

Substitute:

- External APIs (`IHttpClient`, `IApiClient`).
- Database / repository / `DbContext` abstractions.
- File system (`IFileSystem` — see [reference/filesystem.md](filesystem.md)).
- Network / messaging (`IEmailService`, `IMessageQueue`).
- Clocks and randomness (`TimeProvider` — see [reference/datetime.md](datetime.md), `IRandom`).
- Expensive pure computations behind an interface.

Do not substitute:

- Value types (`DateTime`, primitives) — abstract them behind an interface.
- Plain DTOs.
- Pure utilities like AutoMapper's `IMapper` — use a real instance configured the same way as production.

## Common pitfalls

- **Over-specifying interactions** — every `Received(...)` couples the test to internals. Verify only externally observable behaviour.
- **Substituting a concrete class** — disallowed by SKILL.md. If you can't substitute, the dependency isn't behind an abstraction yet.
- **Forgetting `await` on async verification** — `repository.Received(1).SaveAsync(user)` without `await` silently doesn't verify.
- **Mixing concrete args and matchers** — once any argument uses `Arg.Any`/`Arg.Is`, all of them must be matchers.
- **Asserting on log strings** — see ILogger note above; banned by SKILL.md unless logs are the contract.
- **Reflection on private members** — banned. If a method's behaviour can't be observed publicly, restructure the code.

## Templates

- [templates/nsubstitute/mock-patterns.cs](../templates/nsubstitute/mock-patterns.cs) — Dummy/Stub/Fake/Spy/Mock plus async, sequence, conditional, exception patterns.
- [templates/nsubstitute/verification-examples.cs](../templates/nsubstitute/verification-examples.cs) — `Received`/`DidNotReceive`/`InOrder` and full argument-matcher coverage.
