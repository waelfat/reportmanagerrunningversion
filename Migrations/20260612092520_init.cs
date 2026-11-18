using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace reportmangerv2.Migrations
{
    /// <inheritdoc />
    public partial class init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RV2_ASPNETROLES",
                columns: table => new
                {
                    ID = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    NAME = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: true),
                    NORMALIZEDNAME = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: true),
                    CONCURRENCYSTAMP = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RV2_ASPNETROLES", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "RV2_ASPNETUSERS",
                columns: table => new
                {
                    ID = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    FULLNAME = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    ISACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    USERNAME = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: true),
                    NORMALIZEDUSERNAME = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: true),
                    EMAIL = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: true),
                    NORMALIZEDEMAIL = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: true),
                    EMAILCONFIRMED = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    PASSWORDHASH = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    SECURITYSTAMP = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    CONCURRENCYSTAMP = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    PHONENUMBER = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    PHONENUMBERCONFIRMED = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    TWOFACTORENABLED = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    LOCKOUTEND = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: true),
                    LOCKOUTENABLED = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    ACCESSFAILEDCOUNT = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RV2_ASPNETUSERS", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "RV2_SCHEMAS",
                columns: table => new
                {
                    ID = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    CREATEDAT = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    NAME = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    DESCRIPTION = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    HOST = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    PORT = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    SERVICENAME = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    USERID = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    PASSWORD = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RV2_SCHEMAS", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "RV2_ASPNETROLECLAIMS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    ROLEID = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    CLAIMTYPE = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    CLAIMVALUE = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RV2_ASPNETROLECLAIMS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_RV2_ASPNETROLECLAIMS_RV2_A~",
                        column: x => x.ROLEID,
                        principalTable: "RV2_ASPNETROLES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RV2_ASPNETUSERCLAIMS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    USERID = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    CLAIMTYPE = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    CLAIMVALUE = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RV2_ASPNETUSERCLAIMS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_RV2_ASPNETUSERCLAIMS_RV2_A~",
                        column: x => x.USERID,
                        principalTable: "RV2_ASPNETUSERS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RV2_ASPNETUSERLOGINS",
                columns: table => new
                {
                    LOGINPROVIDER = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    PROVIDERKEY = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    PROVIDERDISPLAYNAME = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    USERID = table.Column<string>(type: "NVARCHAR2(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RV2_ASPNETUSERLOGINS", x => new { x.LOGINPROVIDER, x.PROVIDERKEY });
                    table.ForeignKey(
                        name: "FK_RV2_ASPNETUSERLOGINS_RV2_A~",
                        column: x => x.USERID,
                        principalTable: "RV2_ASPNETUSERS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RV2_ASPNETUSERROLES",
                columns: table => new
                {
                    USERID = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    ROLEID = table.Column<string>(type: "NVARCHAR2(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RV2_ASPNETUSERROLES", x => new { x.USERID, x.ROLEID });
                    table.ForeignKey(
                        name: "FK_RV2_ASPNETUSERROLES_RV2_AS~",
                        column: x => x.ROLEID,
                        principalTable: "RV2_ASPNETROLES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RV2_ASPNETUSERROLES_RV2_A~1",
                        column: x => x.USERID,
                        principalTable: "RV2_ASPNETUSERS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RV2_ASPNETUSERTOKENS",
                columns: table => new
                {
                    USERID = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    LOGINPROVIDER = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    NAME = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    VALUE = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RV2_ASPNETUSERTOKENS", x => new { x.USERID, x.LOGINPROVIDER, x.NAME });
                    table.ForeignKey(
                        name: "FK_RV2_ASPNETUSERTOKENS_RV2_A~",
                        column: x => x.USERID,
                        principalTable: "RV2_ASPNETUSERS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RV2_CATEGORIES",
                columns: table => new
                {
                    ID = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    NAME = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    CREATEDAT = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CREATEDBYID = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    PARENTCATEGORYID = table.Column<string>(type: "NVARCHAR2(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RV2_CATEGORIES", x => x.ID);
                    table.ForeignKey(
                        name: "FK_RV2_CATEGORIES_RV2_ASPNETU~",
                        column: x => x.CREATEDBYID,
                        principalTable: "RV2_ASPNETUSERS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RV2_CATEGORIES_RV2_CATEGOR~",
                        column: x => x.PARENTCATEGORYID,
                        principalTable: "RV2_CATEGORIES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RV2_SCHEDULEDJOBS",
                columns: table => new
                {
                    ID = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    PROCEDURENAME = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    JOBTYPE = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false, defaultValue: "StoredProcedure"),
                    SQLSTATEMENT = table.Column<string>(type: "CLOB", nullable: true),
                    DESCRIPTION = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    CRONEXPRESSION = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    SCHEDULEDTYPE = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    ISACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CREATEDAT = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    LASTRUNAT = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    NEXTRUNAT = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    CREATEDBYID = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    Parameters = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    SCHEMAID = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    JOBSTATUS = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    MESSAGEBODY = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    MESSAGESUBJECT = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    SENDTOEMAILS = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    CCMAILS = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RV2_SCHEDULEDJOBS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_RV2_SCHEDULEDJOBS_RV2_ASPN~",
                        column: x => x.CREATEDBYID,
                        principalTable: "RV2_ASPNETUSERS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RV2_SCHEDULEDJOBS_RV2_SCHE~",
                        column: x => x.SCHEMAID,
                        principalTable: "RV2_SCHEMAS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RV2_REPORTS",
                columns: table => new
                {
                    ID = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    NAME = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    DESCRIPTION = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    REPORTQUERY = table.Column<string>(type: "CLOB", nullable: false),
                    CREATEDDATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    MODIFIEDDATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    CREATEDBYID = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    SCHEMAID = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    ISACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CATEGORYID = table.Column<string>(type: "NVARCHAR2(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RV2_REPORTS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_RV2_REPORTS_RV2_ASPNETUSER~",
                        column: x => x.CREATEDBYID,
                        principalTable: "RV2_ASPNETUSERS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RV2_REPORTS_RV2_CATEGORIES~",
                        column: x => x.CATEGORYID,
                        principalTable: "RV2_CATEGORIES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RV2_REPORTS_RV2_SCHEMAS_SC~",
                        column: x => x.SCHEMAID,
                        principalTable: "RV2_SCHEMAS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RV2_EXECUTIONS",
                columns: table => new
                {
                    ID = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    REPORTID = table.Column<string>(type: "NVARCHAR2(450)", nullable: true),
                    EXECUTIONDATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    EXECUTIONTYPE = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    EXECUTIONSTATUS = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    USERID = table.Column<string>(type: "NVARCHAR2(450)", nullable: true),
                    SCHEDULEDJOBID = table.Column<string>(type: "NVARCHAR2(450)", nullable: true),
                    RESULTFILEPATH = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    ERRORMESSAGE = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    DURATION = table.Column<TimeSpan>(type: "INTERVAL DAY(8) TO SECOND(7)", nullable: false),
                    ExecutionParameters = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    EMAILSENT = table.Column<bool>(type: "NUMBER(1)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RV2_EXECUTIONS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_RV2_EXECUTIONS_RV2_ASPNETU~",
                        column: x => x.USERID,
                        principalTable: "RV2_ASPNETUSERS",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_RV2_EXECUTIONS_RV2_REPORTS~",
                        column: x => x.REPORTID,
                        principalTable: "RV2_REPORTS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RV2_EXECUTIONS_RV2_SCHEDUL~",
                        column: x => x.SCHEDULEDJOBID,
                        principalTable: "RV2_SCHEDULEDJOBS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RV2_FAVORITES",
                columns: table => new
                {
                    USERID = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    REPORTID = table.Column<string>(type: "NVARCHAR2(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RV2_FAVORITES", x => new { x.USERID, x.REPORTID });
                    table.ForeignKey(
                        name: "FK_RV2_FAVORITES_RV2_ASPNETUS~",
                        column: x => x.USERID,
                        principalTable: "RV2_ASPNETUSERS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RV2_FAVORITES_RV2_REPORTS_~",
                        column: x => x.REPORTID,
                        principalTable: "RV2_REPORTS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RV2_REPORTPARAMETERS",
                columns: table => new
                {
                    NAME = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    REPORTID = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    ID = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    DESCRIPTION = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    TYPE = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    VIEWCONTROL = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    DEFAULTVALUE = table.Column<string>(type: "CLOB", nullable: true),
                    POSITION = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    ISREQUIRED = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    DEPENDSON = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    DEPENDENCYQUERY = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RV2_REPORTPARAMETERS", x => new { x.REPORTID, x.NAME });
                    table.ForeignKey(
                        name: "FK_RV2_REPORTPARAMETERS_RV2_R~",
                        column: x => x.REPORTID,
                        principalTable: "RV2_REPORTS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "RV2_XRV2_ASPNETROLECLAIMS_OLEI",
                table: "RV2_ASPNETROLECLAIMS",
                column: "ROLEID");

            migrationBuilder.CreateIndex(
                name: "RV2_XRV2_ASPNETROLES_DNAME",
                table: "RV2_ASPNETROLES",
                column: "NORMALIZEDNAME",
                unique: true,
                filter: "\"NORMALIZEDNAME\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "RV2_XRV2_ASPNETUSERCLAIMS_SERI",
                table: "RV2_ASPNETUSERCLAIMS",
                column: "USERID");

            migrationBuilder.CreateIndex(
                name: "RV2_XRV2_ASPNETUSERLOGINS_SERI",
                table: "RV2_ASPNETUSERLOGINS",
                column: "USERID");

            migrationBuilder.CreateIndex(
                name: "RV2_XRV2_ASPNETUSERROLES_OLEID",
                table: "RV2_ASPNETUSERROLES",
                column: "ROLEID");

            migrationBuilder.CreateIndex(
                name: "RV2_XRV2_ASPNETUSERS_EMAIL",
                table: "RV2_ASPNETUSERS",
                column: "NORMALIZEDEMAIL");

            migrationBuilder.CreateIndex(
                name: "RV2_XRV2_ASPNETUSERS_RNAME",
                table: "RV2_ASPNETUSERS",
                column: "NORMALIZEDUSERNAME",
                unique: true,
                filter: "\"NORMALIZEDUSERNAME\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RV2_CATEGORIES_NAME",
                table: "RV2_CATEGORIES",
                column: "NAME");

            migrationBuilder.CreateIndex(
                name: "IX_RV2_CATEGORIES_NAME_PARENT~",
                table: "RV2_CATEGORIES",
                columns: new[] { "NAME", "PARENTCATEGORYID" },
                unique: true,
                filter: "\"PARENTCATEGORYID\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "RV2_XRV2_CATEGORIES_DBYID",
                table: "RV2_CATEGORIES",
                column: "CREATEDBYID");

            migrationBuilder.CreateIndex(
                name: "RV2_XRV2_CATEGORIES_ORYID",
                table: "RV2_CATEGORIES",
                column: "PARENTCATEGORYID");

            migrationBuilder.CreateIndex(
                name: "RV2_XRV2_EXECUTIONS_JOBID",
                table: "RV2_EXECUTIONS",
                column: "SCHEDULEDJOBID");

            migrationBuilder.CreateIndex(
                name: "RV2_XRV2_EXECUTIONS_ORTID",
                table: "RV2_EXECUTIONS",
                column: "REPORTID");

            migrationBuilder.CreateIndex(
                name: "RV2_XRV2_EXECUTIONS_SERID",
                table: "RV2_EXECUTIONS",
                column: "USERID");

            migrationBuilder.CreateIndex(
                name: "RV2_XRV2_FAVORITES_ORTID",
                table: "RV2_FAVORITES",
                column: "REPORTID");

            migrationBuilder.CreateIndex(
                name: "RV2_XRV2_REPORTS_DBYID",
                table: "RV2_REPORTS",
                column: "CREATEDBYID");

            migrationBuilder.CreateIndex(
                name: "RV2_XRV2_REPORTS_EMAID",
                table: "RV2_REPORTS",
                column: "SCHEMAID");

            migrationBuilder.CreateIndex(
                name: "RV2_XRV2_REPORTS_ORYID",
                table: "RV2_REPORTS",
                column: "CATEGORYID");

            migrationBuilder.CreateIndex(
                name: "RV2_XRV2_SCHEDULEDJOBS_DBYID",
                table: "RV2_SCHEDULEDJOBS",
                column: "CREATEDBYID");

            migrationBuilder.CreateIndex(
                name: "RV2_XRV2_SCHEDULEDJOBS_EMAID",
                table: "RV2_SCHEDULEDJOBS",
                column: "SCHEMAID");

            migrationBuilder.CreateIndex(
                name: "IX_RV2_SCHEMAS_NAME",
                table: "RV2_SCHEMAS",
                column: "NAME",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RV2_ASPNETROLECLAIMS");

            migrationBuilder.DropTable(
                name: "RV2_ASPNETUSERCLAIMS");

            migrationBuilder.DropTable(
                name: "RV2_ASPNETUSERLOGINS");

            migrationBuilder.DropTable(
                name: "RV2_ASPNETUSERROLES");

            migrationBuilder.DropTable(
                name: "RV2_ASPNETUSERTOKENS");

            migrationBuilder.DropTable(
                name: "RV2_EXECUTIONS");

            migrationBuilder.DropTable(
                name: "RV2_FAVORITES");

            migrationBuilder.DropTable(
                name: "RV2_REPORTPARAMETERS");

            migrationBuilder.DropTable(
                name: "RV2_ASPNETROLES");

            migrationBuilder.DropTable(
                name: "RV2_SCHEDULEDJOBS");

            migrationBuilder.DropTable(
                name: "RV2_REPORTS");

            migrationBuilder.DropTable(
                name: "RV2_CATEGORIES");

            migrationBuilder.DropTable(
                name: "RV2_SCHEMAS");

            migrationBuilder.DropTable(
                name: "RV2_ASPNETUSERS");
        }
    }
}
