using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ItalianApp.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NameFr = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    NameIt = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IconKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PhoneticTips",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LabelFr = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AdviceFr = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    PhonemeSymbols = table.Column<string>(type: "jsonb", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhoneticTips", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Scenarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    TitleFr = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    TitleIt = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    DescriptionFr = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Scenarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Scenarios_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DailyUsage",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    ScoringCalls = table.Column<int>(type: "integer", nullable: false),
                    LlmCalls = table.Column<int>(type: "integer", nullable: false),
                    TtsCalls = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyUsage", x => new { x.UserId, x.Date });
                    table.ForeignKey(
                        name: "FK_DailyUsage_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Phrases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScenarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    TextIt = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    TextFr = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ContextFr = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Difficulty = table.Column<int>(type: "integer", nullable: false),
                    PhoneticTraps = table.Column<string>(type: "jsonb", nullable: false),
                    AudioUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    TtsVoice = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Phrases", x => x.Id);
                    table.CheckConstraint("CK_Phrases_Difficulty", "\"Difficulty\" BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_Phrases_Scenarios_ScenarioId",
                        column: x => x.ScenarioId,
                        principalTable: "Scenarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Attempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhraseId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OverallScore = table.Column<double>(type: "double precision", nullable: false),
                    AccuracyScore = table.Column<double>(type: "double precision", nullable: false),
                    FluencyScore = table.Column<double>(type: "double precision", nullable: false),
                    CompletenessScore = table.Column<double>(type: "double precision", nullable: false),
                    ProsodyScore = table.Column<double>(type: "double precision", nullable: false),
                    PhonemeScores = table.Column<string>(type: "jsonb", nullable: false),
                    FeedbackText = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    FeedbackSource = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Attempts_Phrases_PhraseId",
                        column: x => x.PhraseId,
                        principalTable: "Phrases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Attempts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PhraseProgress",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhraseId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    BestScore = table.Column<double>(type: "double precision", nullable: false),
                    LastScore = table.Column<double>(type: "double precision", nullable: false),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NextReviewAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EaseFactor = table.Column<double>(type: "double precision", nullable: false),
                    Repetitions = table.Column<int>(type: "integer", nullable: false),
                    IntervalDays = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhraseProgress", x => new { x.UserId, x.PhraseId });
                    table.ForeignKey(
                        name: "FK_PhraseProgress_Phrases_PhraseId",
                        column: x => x.PhraseId,
                        principalTable: "Phrases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PhraseProgress_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Attempts_PhraseId",
                table: "Attempts",
                column: "PhraseId");

            migrationBuilder.CreateIndex(
                name: "IX_Attempts_UserId_AttemptedAt",
                table: "Attempts",
                columns: new[] { "UserId", "AttemptedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Attempts_UserId_PhraseId_AttemptedAt",
                table: "Attempts",
                columns: new[] { "UserId", "PhraseId", "AttemptedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_DisplayOrder",
                table: "Categories",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Slug",
                table: "Categories",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyUsage_Date",
                table: "DailyUsage",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_PhraseProgress_PhraseId",
                table: "PhraseProgress",
                column: "PhraseId");

            migrationBuilder.CreateIndex(
                name: "IX_PhraseProgress_UserId_LastScore",
                table: "PhraseProgress",
                columns: new[] { "UserId", "LastScore" });

            migrationBuilder.CreateIndex(
                name: "IX_PhraseProgress_UserId_NextReviewAt",
                table: "PhraseProgress",
                columns: new[] { "UserId", "NextReviewAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Phrases_ScenarioId_ReviewedAt_Difficulty",
                table: "Phrases",
                columns: new[] { "ScenarioId", "ReviewedAt", "Difficulty" });

            migrationBuilder.CreateIndex(
                name: "IX_Phrases_ScenarioId_TextIt",
                table: "Phrases",
                columns: new[] { "ScenarioId", "TextIt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Scenarios_CategoryId_DisplayOrder",
                table: "Scenarios",
                columns: new[] { "CategoryId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Attempts");

            migrationBuilder.DropTable(
                name: "DailyUsage");

            migrationBuilder.DropTable(
                name: "PhoneticTips");

            migrationBuilder.DropTable(
                name: "PhraseProgress");

            migrationBuilder.DropTable(
                name: "Phrases");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Scenarios");

            migrationBuilder.DropTable(
                name: "Categories");
        }
    }
}
