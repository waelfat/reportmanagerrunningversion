using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace reportmangerv2.Migrations
{
    /// <inheritdoc />
    public partial class AddReportTypeAndDirection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "REPORTTYPE",
                table: "RV2_REPORTS",
                type: "NVARCHAR2(2000)",
                nullable: false,
                defaultValue: "SQLSTATEMENT");

            migrationBuilder.AddColumn<string>(
                name: "DIRECTION",
                table: "RV2_REPORTPARAMETERS",
                type: "NVARCHAR2(2000)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "REPORTTYPE",
                table: "RV2_REPORTS");

            migrationBuilder.DropColumn(
                name: "DIRECTION",
                table: "RV2_REPORTPARAMETERS");
        }
    }
}
