using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using Xunit;

namespace DotNetTesting.ComplexObjectComparison.Templates;

/// <summary>
/// Common patterns for deep object comparison with <c>BeEquivalentTo</c>.
/// </summary>
public class ComparisonPatterns
{
    #region Test models

    public class Order
    {
        public int Id { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<OrderItem> Items { get; set; } = new();
        public AuditInfo AuditInfo { get; set; } = new();
    }

    public class OrderItem
    {
        public int Id { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public DateTime AddedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
    }

    public class AuditInfo
    {
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
    }

    public class TreeNode
    {
        public string Value { get; set; } = string.Empty;
        public TreeNode? Parent { get; set; }
        public List<TreeNode> Children { get; set; } = new();
    }

    public class DataRecord
    {
        public int Id { get; set; }
        public string Value { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public bool IsProcessed { get; set; }
    }

    #endregion

    #region Pattern 1 — basic deep equivalence

    [Fact]
    public void Pattern1_DeepObjectEquivalence()
    {
        // Arrange
        var expected = new Order
        {
            Id = 1,
            CustomerName = "John Doe",
            TotalAmount = 1059.97m,
            Items = new List<OrderItem>
            {
                new() { Id = 1, ProductName = "Laptop", Quantity = 1, Price = 999.99m },
                new() { Id = 2, ProductName = "Mouse",  Quantity = 2, Price = 29.99m }
            }
        };

        // Act
        var actual = GetOrderFromService(1);

        // Assert — full deep equivalence
        actual.Should().BeEquivalentTo(expected);
    }

    #endregion

    #region Pattern 2 — excluding volatile fields

    [Fact]
    public void Pattern2_ExcludingTimestamps()
    {
        var original = new Order
        {
            Id = 1,
            CustomerName = "John Doe",
            TotalAmount = 999.99m,
            CreatedAt = DateTime.Now.AddDays(-1),
            UpdatedAt = DateTime.Now.AddDays(-1)
        };

        var updated = UpdateOrder(original);

        updated.Should().BeEquivalentTo(original, options =>
            options.Excluding(o => o.UpdatedAt)
                   .Excluding(o => o.Status));

        // Still pin the volatile field — just separately.
        updated.UpdatedAt.Should().BeAfter(original.UpdatedAt);
    }

    #endregion

    #region Pattern 3 — nested object exclusion via path predicate

    [Fact]
    public void Pattern3_NestedTimestampExclusion()
    {
        var expected = new Order
        {
            Id = 1,
            CustomerName = "John Doe",
            CreatedAt = DateTime.Now,
            Items = new List<OrderItem>
            {
                new() { Id = 1, ProductName = "Laptop", AddedAt = DateTime.Now }
            },
            AuditInfo = new AuditInfo
            {
                CreatedBy = "system",
                CreatedAt = DateTime.Now
            }
        };

        var actual = GetOrderFromService(1);

        // Drop every "At" / "Time" field across the graph.
        actual.Should().BeEquivalentTo(expected, options =>
            options.Excluding(ctx => ctx.Path.EndsWith("At"))
                   .Excluding(ctx => ctx.Path.EndsWith("Time")));
    }

    #endregion

    #region Pattern 4 — cyclic references

    [Fact]
    public void Pattern4_CyclicReferences()
    {
        var parent = new TreeNode { Value = "Root" };
        var child1 = new TreeNode { Value = "Child1", Parent = parent };
        var child2 = new TreeNode { Value = "Child2", Parent = parent };
        parent.Children = new List<TreeNode> { child1, child2 };

        var actualTree = GetTreeFromService("Root");

        actualTree.Should().BeEquivalentTo(parent, options =>
            options.IgnoringCyclicReferences()
                   .WithMaxRecursionDepth(10));
    }

    #endregion

    #region Pattern 5 — whitelist key properties

    [Fact]
    public void Pattern5_OnlyKeyProperties()
    {
        var expected = new Order
        {
            Id = 1,
            CustomerName = "John Doe",
            TotalAmount = 999.99m
        };

        var actual = GetOrderFromService(1);

        actual.Should().BeEquivalentTo(expected, options =>
            options.Including(o => o.Id)
                   .Including(o => o.CustomerName)
                   .Including(o => o.TotalAmount));
    }

    #endregion

    #region Pattern 6 — collection ordering

    [Fact]
    public void Pattern6_OrderingControl()
    {
        var expectedItems = new[]
        {
            new OrderItem { Id = 1, ProductName = "Laptop" },
            new OrderItem { Id = 2, ProductName = "Mouse"  }
        };

        var orderedItems = GetOrderedItems();
        var unorderedItems = GetUnorderedItems();

        orderedItems.Should().BeEquivalentTo(expectedItems, options =>
            options.WithStrictOrdering());

        unorderedItems.Should().BeEquivalentTo(expectedItems, options =>
            options.WithoutStrictOrdering());
    }

    #endregion

    #region Pattern 7 — large dataset sampling

    [Fact]
    public void Pattern7_LargeDatasetSampling()
    {
        var largeDataset = Enumerable.Range(1, 100_000)
            .Select(i => new DataRecord
            {
                Id = i,
                Value = $"Record_{i}",
                Timestamp = DateTime.Now
            })
            .ToList();

        var processed = ProcessLargeDataset(largeDataset);

        // 1. count first
        processed.Should().HaveCount(largeDataset.Count);

        // 2. sample-based equivalence
        var sampleSize = 1000;
        var sampleIndices = Enumerable.Range(0, sampleSize)
            .Select(_ => Random.Shared.Next(processed.Count))
            .Distinct()
            .ToList();

        foreach (var index in sampleIndices)
        {
            processed[index].Should().BeEquivalentTo(largeDataset[index], options =>
                options.Excluding(r => r.Timestamp)
                       .Excluding(r => r.IsProcessed));
        }

        // 3. aggregate statistic
        processed.Count(r => r.IsProcessed).Should().Be(processed.Count);
    }

    #endregion

    #region Pattern 8 — EF entity comparison

    [Fact]
    public void Pattern8_EntityFrameworkComparison()
    {
        var expected = new Order
        {
            Id = 1,
            CustomerName = "John Doe",
            TotalAmount = 999.99m
        };

        var actual = GetOrderFromDatabase(1);

        actual.Should().BeEquivalentTo(expected, options =>
            options.ExcludingMissingMembers()       // ignore extra EF members
                   .Excluding(o => o.CreatedAt)
                   .Excluding(o => o.UpdatedAt)
                   .Excluding(o => o.AuditInfo));   // possible navigation
    }

    #endregion

    #region Pattern 9 — AssertionScope batching

    [Fact]
    public void Pattern9_AssertionScopeBatching()
    {
        var orders = GetMultipleOrders();

        using (new AssertionScope())
        {
            foreach (var order in orders)
            {
                order.Id.Should().BeGreaterThan(0, "Order Id must be > 0");
                order.CustomerName.Should().NotBeNullOrEmpty("CustomerName is required");
                order.TotalAmount.Should().BeGreaterThan(0, "TotalAmount must be > 0");
                order.Items.Should().NotBeEmpty("Order must contain at least one item");
            }
        }
    }

    #endregion

    #region Pattern 10 — meaningful failure context with `because`

    [Fact]
    public void Pattern10_MeaningfulFailureContext()
    {
        var expected = new Order
        {
            Id = 1,
            CustomerName = "John Doe",
            Status = "Pending"
        };

        var actual = GetOrderFromService(1);

        actual.Should().BeEquivalentTo(expected, options =>
            options.Excluding(o => o.CreatedAt)
                   .Because("CreatedAt is server-generated")
                   .Excluding(o => o.UpdatedAt)
                   .Because("UpdatedAt changes on every save"));

        actual.Status.Should().Be(expected.Status,
            "because a newly created order defaults to Pending");
    }

    #endregion

    #region Helper stubs

    private Order GetOrderFromService(int id) => new()
    {
        Id = id,
        CustomerName = "John Doe",
        TotalAmount = 1059.97m,
        CreatedAt = DateTime.Now,
        UpdatedAt = DateTime.Now,
        Status = "Pending",
        Items = new List<OrderItem>
        {
            new() { Id = 1, ProductName = "Laptop", Quantity = 1, Price = 999.99m, AddedAt = DateTime.Now },
            new() { Id = 2, ProductName = "Mouse",  Quantity = 2, Price = 29.99m,  AddedAt = DateTime.Now }
        },
        AuditInfo = new AuditInfo
        {
            CreatedBy = "system",
            CreatedAt = DateTime.Now
        }
    };

    private Order UpdateOrder(Order order)
    {
        order.UpdatedAt = DateTime.Now;
        order.Status = "Processing";
        return order;
    }

    private TreeNode GetTreeFromService(string value)
    {
        var parent = new TreeNode { Value = value };
        var child1 = new TreeNode { Value = "Child1", Parent = parent };
        var child2 = new TreeNode { Value = "Child2", Parent = parent };
        parent.Children = new List<TreeNode> { child1, child2 };
        return parent;
    }

    private List<OrderItem> GetOrderedItems() => new()
    {
        new() { Id = 1, ProductName = "Laptop" },
        new() { Id = 2, ProductName = "Mouse"  }
    };

    private List<OrderItem> GetUnorderedItems() => new()
    {
        new() { Id = 2, ProductName = "Mouse"  },
        new() { Id = 1, ProductName = "Laptop" }
    };

    private List<DataRecord> ProcessLargeDataset(List<DataRecord> data) =>
        data.Select(r => new DataRecord
        {
            Id = r.Id,
            Value = r.Value,
            Timestamp = DateTime.Now,
            IsProcessed = true
        }).ToList();

    private Order GetOrderFromDatabase(int id) => GetOrderFromService(id);

    private List<Order> GetMultipleOrders() => new()
    {
        new() { Id = 1, CustomerName = "John", TotalAmount = 999,  Items = new() { new() { ProductName = "Item1" } } },
        new() { Id = 2, CustomerName = "Jane", TotalAmount = 1500, Items = new() { new() { ProductName = "Item2" } } }
    };

    #endregion
}
