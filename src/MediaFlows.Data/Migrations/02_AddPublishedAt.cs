using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaFlows.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPublishedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "MediaAssets",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "MediaAssets");
        }
    }
}
