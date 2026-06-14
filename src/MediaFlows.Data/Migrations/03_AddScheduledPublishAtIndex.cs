using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaFlows.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledPublishAtIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_Status_ScheduledPublishAt",
                table: "MediaAssets",
                columns: new[] { "Status", "ScheduledPublishAt" },
                filter: "\"ScheduledPublishAt\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MediaAssets_Status_ScheduledPublishAt",
                table: "MediaAssets");
        }
    }
}
