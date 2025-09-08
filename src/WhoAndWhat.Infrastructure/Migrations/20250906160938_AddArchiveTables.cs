using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhoAndWhat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddArchiveTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArchivedProjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Progress = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ArchiveReason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ArchivedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    TotalTasksCount = table.Column<int>(type: "integer", nullable: false),
                    CompletedTasksCount = table.Column<int>(type: "integer", nullable: false),
                    TotalDuration = table.Column<TimeSpan>(type: "interval", nullable: true),
                    TasksJson = table.Column<string>(type: "jsonb", nullable: true),
                    ContactsJson = table.Column<string>(type: "jsonb", nullable: true),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivedProjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchivedProjects_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ArchivedTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ArchiveReason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ArchivedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ParentTaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParentTaskTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SubtasksJson = table.Column<string>(type: "jsonb", nullable: true),
                    ContactsJson = table.Column<string>(type: "jsonb", nullable: true),
                    AttachmentsJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivedTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchivedTasks_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedProjects_ArchivedAt",
                table: "ArchivedProjects",
                column: "ArchivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedProjects_OriginalProjectId",
                table: "ArchivedProjects",
                column: "OriginalProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedProjects_UserId",
                table: "ArchivedProjects",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedProjects_UserId_ArchivedAt",
                table: "ArchivedProjects",
                columns: new[] { "UserId", "ArchivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedProjects_UserId_Status",
                table: "ArchivedProjects",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedTasks_ArchivedAt",
                table: "ArchivedTasks",
                column: "ArchivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedTasks_OriginalTaskId",
                table: "ArchivedTasks",
                column: "OriginalTaskId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedTasks_ParentTaskId",
                table: "ArchivedTasks",
                column: "ParentTaskId",
                filter: "parent_task_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedTasks_ProjectId",
                table: "ArchivedTasks",
                column: "ProjectId",
                filter: "project_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedTasks_UserId",
                table: "ArchivedTasks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedTasks_UserId_ArchivedAt",
                table: "ArchivedTasks",
                columns: new[] { "UserId", "ArchivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedTasks_UserId_Category",
                table: "ArchivedTasks",
                columns: new[] { "UserId", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedTasks_UserId_Status",
                table: "ArchivedTasks",
                columns: new[] { "UserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchivedProjects");

            migrationBuilder.DropTable(
                name: "ArchivedTasks");
        }
    }
}
