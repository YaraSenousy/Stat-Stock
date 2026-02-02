using FluentAssertions;
using StatStock.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace StatStock.UnitTests.Domain;

public class SupplierTests
{
    [Fact]
    public void Supplier_ShouldInitializeWithDefaultValues()
    {
        // Arrange & Act
        var supplier = new Supplier();

        // Assert
        supplier.Id.Should().Be(0);
        supplier.Name.Should().Be(string.Empty);
        supplier.Contact.Should().Be(string.Empty);
        supplier.Email.Should().Be(string.Empty);
        supplier.Phone.Should().Be(string.Empty);
        supplier.Address.Should().Be(string.Empty);
        supplier.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        supplier.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        supplier.Orders.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Supplier_ShouldSetProperties_WhenValidDataProvided()
    {
        // Arrange
        var createdAt = DateTime.UtcNow.AddDays(-10);
        var updatedAt = DateTime.UtcNow;

        // Act
        var supplier = new Supplier
        {
            Id = 1,
            Name = "Tech Supplies Inc",
            Contact = "John Doe",
            Email = "john@techsupplies.com",
            Phone = "+1-555-0123",
            Address = "123 Tech Street, Silicon Valley, CA 94000",
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };

        // Assert
        supplier.Id.Should().Be(1);
        supplier.Name.Should().Be("Tech Supplies Inc");
        supplier.Contact.Should().Be("John Doe");
        supplier.Email.Should().Be("john@techsupplies.com");
        supplier.Phone.Should().Be("+1-555-0123");
        supplier.Address.Should().Be("123 Tech Street, Silicon Valley, CA 94000");
        supplier.CreatedAt.Should().Be(createdAt);
        supplier.UpdatedAt.Should().Be(updatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Supplier_Name_ShouldBeRequired(string? name)
    {
        // Arrange
        var supplier = new Supplier
        {
            Name = name!,
            Contact = "John Doe",
            Email = "john@test.com",
            Phone = "+1-555-0123"
        };

        // Act
        var validationResults = ValidateModel(supplier);

        // Assert
        validationResults.Should().Contain(r => r.MemberNames.Contains("Name"));
    }

    [Fact]
    public void Supplier_Name_ShouldNotExceed200Characters()
    {
        // Arrange
        var supplier = new Supplier
        {
            Name = new string('A', 201),
            Contact = "John Doe",
            Email = "john@test.com",
            Phone = "+1-555-0123"
        };

        // Act
        var validationResults = ValidateModel(supplier);

        // Assert
        validationResults.Should().Contain(r => 
            r.MemberNames.Contains("Name") && 
            r.ErrorMessage!.Contains("200"));
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("@test.com")]
    [InlineData("test@")]
    [InlineData("test")]
    public void Supplier_Email_ShouldBeValidFormat(string email)
    {
        // Arrange
        var supplier = new Supplier
        {
            Name = "Test Supplier",
            Contact = "John Doe",
            Email = email,
            Phone = "+1-555-0123"
        };

        // Act
        var validationResults = ValidateModel(supplier);

        // Assert
        validationResults.Should().Contain(r => r.MemberNames.Contains("Email"));
    }

    [Theory]
    [InlineData("+1-555-0123")]
    [InlineData("555-0123")]
    [InlineData("(555) 0123")]
    [InlineData("+44 20 7946 0958")]
    [InlineData("1234567890")]
    public void Supplier_Phone_ShouldAcceptValidFormats(string phone)
    {
        // Arrange
        var supplier = new Supplier
        {
            Name = "Test Supplier",
            Contact = "John Doe",
            Email = "test@supplier.com",
            Phone = phone
        };

        // Act
        var validationResults = ValidateModel(supplier);

        // Assert
        validationResults.Where(r => r.MemberNames.Contains("Phone"))
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("phone#123")]
    [InlineData("test@phone")]
    public void Supplier_Phone_ShouldRejectInvalidFormats(string phone)
    {
        // Arrange
        var supplier = new Supplier
        {
            Name = "Test Supplier",
            Contact = "John Doe",
            Email = "test@supplier.com",
            Phone = phone
        };

        // Act
        var validationResults = ValidateModel(supplier);

        // Assert
        validationResults.Should().Contain(r => r.MemberNames.Contains("Phone"));
    }

    [Fact]
    public void Supplier_Address_CanBeEmpty()
    {
        // Arrange
        var supplier = new Supplier
        {
            Name = "Test Supplier",
            Contact = "John Doe",
            Email = "test@supplier.com",
            Phone = "+1-555-0123",
            Address = ""
        };

        // Act
        var validationResults = ValidateModel(supplier);

        // Assert
        validationResults.Where(r => r.MemberNames.Contains("Address"))
            .Should().BeEmpty();
    }

    [Fact]
    public void Supplier_Address_ShouldNotExceed500Characters()
    {
        // Arrange
        var supplier = new Supplier
        {
            Name = "Test Supplier",
            Contact = "John Doe",
            Email = "test@supplier.com",
            Phone = "+1-555-0123",
            Address = new string('A', 501)
        };

        // Act
        var validationResults = ValidateModel(supplier);

        // Assert
        validationResults.Should().Contain(r => 
            r.MemberNames.Contains("Address") && 
            r.ErrorMessage!.Contains("500"));
    }

    [Fact]
    public void Supplier_Orders_ShouldBeNavigableCollection()
    {
        // Arrange
        var supplier = new Supplier
        {
            Name = "Test Supplier",
            Contact = "John Doe",
            Email = "test@supplier.com",
            Phone = "+1-555-0123"
        };

        var order = new Order
        {
            OrderNumber = "ORD-001",
            UserId = "user1"
        };

        // Act
        supplier.Orders.Add(order);

        // Assert
        supplier.Orders.Should().HaveCount(1);
        supplier.Orders.First().Should().Be(order);
    }

    private static IList<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var ctx = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, ctx, validationResults, true);
        return validationResults;
    }
}
