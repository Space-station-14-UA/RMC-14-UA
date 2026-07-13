using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddMriyaSponsors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sponsor_ranks",
                columns: table => new
                {
                    sponsor_ranks_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    default_color = table.Column<string>(type: "TEXT", nullable: false),
                    default_ghost_color = table.Column<string>(type: "TEXT", nullable: true),
                    default_ooc_color = table.Column<string>(type: "TEXT", nullable: true),
                    can_set_ghost_color = table.Column<bool>(type: "INTEGER", nullable: false),
                    can_set_ooc_color = table.Column<bool>(type: "INTEGER", nullable: false),
                    show_in_sponsor_window = table.Column<bool>(type: "INTEGER", nullable: false),
                    priority = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sponsor_ranks", x => x.sponsor_ranks_id);
                });

            migrationBuilder.CreateTable(
                name: "mriya_sponsors",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    selected_ghost_color = table.Column<string>(type: "TEXT", nullable: true),
                    selected_ooc_color = table.Column<string>(type: "TEXT", nullable: true),
                    selected_ghost_rank_id = table.Column<int>(type: "INTEGER", nullable: true),
                    selected_ooc_rank_id = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mriya_sponsors", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_mriya_sponsors_sponsor_ranks_selected_ghost_rank_id",
                        column: x => x.selected_ghost_rank_id,
                        principalTable: "sponsor_ranks",
                        principalColumn: "sponsor_ranks_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_mriya_sponsors_sponsor_ranks_selected_ooc_rank_id",
                        column: x => x.selected_ooc_rank_id,
                        principalTable: "sponsor_ranks",
                        principalColumn: "sponsor_ranks_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "rank_tags",
                columns: table => new
                {
                    rank_tags_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    sponsor_rank_id = table.Column<int>(type: "INTEGER", nullable: false),
                    tag_value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rank_tags", x => x.rank_tags_id);
                    table.ForeignKey(
                        name: "FK_rank_tags_sponsor_ranks_sponsor_rank_id",
                        column: x => x.sponsor_rank_id,
                        principalTable: "sponsor_ranks",
                        principalColumn: "sponsor_ranks_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sponsor_role_assignments",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    rank_id = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sponsor_role_assignments", x => new { x.user_id, x.rank_id });
                    table.ForeignKey(
                        name: "FK_sponsor_role_assignments_mriya_sponsors_sponsor_user_id",
                        column: x => x.user_id,
                        principalTable: "mriya_sponsors",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sponsor_role_assignments_sponsor_ranks_rank_id",
                        column: x => x.rank_id,
                        principalTable: "sponsor_ranks",
                        principalColumn: "sponsor_ranks_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mriya_sponsors_selected_ghost_rank_id",
                table: "mriya_sponsors",
                column: "selected_ghost_rank_id");

            migrationBuilder.CreateIndex(
                name: "IX_mriya_sponsors_selected_ooc_rank_id",
                table: "mriya_sponsors",
                column: "selected_ooc_rank_id");

            migrationBuilder.CreateIndex(
                name: "IX_rank_tags_sponsor_rank_id",
                table: "rank_tags",
                column: "sponsor_rank_id");

            migrationBuilder.CreateIndex(
                name: "IX_sponsor_role_assignments_rank_id",
                table: "sponsor_role_assignments",
                column: "rank_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rank_tags");

            migrationBuilder.DropTable(
                name: "sponsor_role_assignments");

            migrationBuilder.DropTable(
                name: "mriya_sponsors");

            migrationBuilder.DropTable(
                name: "sponsor_ranks");
        }
    }
}
