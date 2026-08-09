using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Commerce.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCartItemPriceSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "UnitPriceWhenAdded",
                table: "CartItems",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnitPriceWhenAdded",
                table: "CartItems");
        }
    }
}
