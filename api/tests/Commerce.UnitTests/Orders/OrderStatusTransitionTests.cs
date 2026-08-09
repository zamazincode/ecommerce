using Commerce.Domain.Common;
using Commerce.Domain.Orders;
using Shouldly;

namespace Commerce.UnitTests.Orders;

public class OrderStatusTransitionTests
{
    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Paid)]
    [InlineData(OrderStatus.Pending, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Paid, OrderStatus.Preparing)]
    [InlineData(OrderStatus.Paid, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Paid, OrderStatus.Refunded)]
    [InlineData(OrderStatus.Preparing, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Delivered)]
    public void CanTransition_WithValidTransition_ReturnsTrue(OrderStatus from, OrderStatus to)
    {
        OrderStatusTransition.CanTransition(from, to).ShouldBeTrue();
    }

    [Theory]
    [InlineData(OrderStatus.Delivered, OrderStatus.Pending)]   // geriye gidiş yok
    [InlineData(OrderStatus.Shipped, OrderStatus.Cancelled)]   // kargodan sonra iptal yok
    [InlineData(OrderStatus.Cancelled, OrderStatus.Paid)]      // iptalden dönüş yok
    [InlineData(OrderStatus.Refunded, OrderStatus.Paid)]
    [InlineData(OrderStatus.Pending, OrderStatus.Shipped)]     // adım atlanamaz
    [InlineData(OrderStatus.Pending, OrderStatus.Delivered)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Cancelled)]
    public void CanTransition_WithInvalidTransition_ReturnsFalse(OrderStatus from, OrderStatus to)
    {
        OrderStatusTransition.CanTransition(from, to).ShouldBeFalse();
    }

    [Fact]
    public void EnsureCanTransition_WithInvalidTransition_ThrowsWithBothStatuses()
    {
        var ex = Should.Throw<InvalidOrderStatusTransitionException>(() =>
            OrderStatusTransition.EnsureCanTransition(OrderStatus.Delivered, OrderStatus.Pending));

        ex.From.ShouldBe(OrderStatus.Delivered);
        ex.To.ShouldBe(OrderStatus.Pending);
        ex.ShouldBeAssignableTo<DomainRuleException>();
    }

    [Fact]
    public void EnsureCanTransition_WithValidTransition_DoesNotThrow()
    {
        Should.NotThrow(() =>
            OrderStatusTransition.EnsureCanTransition(OrderStatus.Pending, OrderStatus.Paid));
    }

    [Theory]
    [InlineData(OrderStatus.Pending, true)]
    [InlineData(OrderStatus.Paid, true)]
    [InlineData(OrderStatus.Preparing, false)]
    [InlineData(OrderStatus.Shipped, false)]
    [InlineData(OrderStatus.Delivered, false)]
    public void IsCancellableByCustomer_MatchesBusinessRule(OrderStatus status, bool expected)
    {
        OrderStatusTransition.IsCancellableByCustomer(status).ShouldBe(expected);
    }

    [Theory]
    [InlineData(OrderStatus.Cancelled, true)]
    [InlineData(OrderStatus.Refunded, true)]
    [InlineData(OrderStatus.Paid, false)]
    [InlineData(OrderStatus.Shipped, false)]
    public void RestoresStock_OnlyForCancelledAndRefunded(OrderStatus to, bool expected)
    {
        OrderStatusTransition.RestoresStock(to).ShouldBe(expected);
    }

    [Fact]
    public void TerminalStatuses_HaveNoAllowedTargets()
    {
        OrderStatusTransition.AllowedTargets(OrderStatus.Delivered).ShouldBeEmpty();
        OrderStatusTransition.AllowedTargets(OrderStatus.Cancelled).ShouldBeEmpty();
        OrderStatusTransition.AllowedTargets(OrderStatus.Refunded).ShouldBeEmpty();
    }

    [Fact]
    public void NoStatus_CanTransitionToItself()
    {
        // "Zaten Paid olan siparişi tekrar Paid yap" gibi çağrılar
        // sessizce geçmemeli — çift ödeme işaretinin habercisi olabilir.
        foreach (var status in Enum.GetValues<OrderStatus>())
            OrderStatusTransition.CanTransition(status, status).ShouldBeFalse();
    }
}
