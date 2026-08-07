using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Commerce.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Sıra kritik: uzantılar ve immutable_unaccent fonksiyonu, onu kullanan
            // GENERATED kolonlardan (aşağıdaki AddColumn'lar) ÖNCE var olmalı.
            // EF'in HasPostgresExtension'dan ürettiği AlterDatabase da aynı uzantıları
            // CREATE EXTENSION IF NOT EXISTS ile kuruyor; burada tekrar kurmak zararsız,
            // ama fonksiyonu EF üretemez — elle eklenmesi zorunlu.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS unaccent;");
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION immutable_unaccent(text)
                RETURNS text
                LANGUAGE sql
                IMMUTABLE STRICT PARALLEL SAFE
                AS $$ SELECT public.unaccent('public.unaccent'::regdictionary, $1) $$;
                """);

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .Annotation("Npgsql:PostgresExtension:unaccent", ",,");

            migrationBuilder.AddColumn<string>(
                name: "SearchName",
                table: "Products",
                type: "text",
                maxLength: 512,
                nullable: true,
                computedColumnSql: "immutable_unaccent(lower(coalesce(\"Name\", '') || ' ' || coalesce(\"AuthorNames\", '')))",
                stored: true);

            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "Products",
                type: "tsvector",
                nullable: true,
                computedColumnSql: "setweight(to_tsvector('turkish', immutable_unaccent(coalesce(\"Name\", ''))), 'A') ||\r\nsetweight(to_tsvector('turkish', immutable_unaccent(coalesce(\"AuthorNames\", ''))), 'B') ||\r\nsetweight(to_tsvector('turkish', immutable_unaccent(coalesce(\"PublisherName\", ''))), 'C') ||\r\nsetweight(to_tsvector('turkish', immutable_unaccent(coalesce(\"Description\", ''))), 'D')",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "ix_products_searchname_trgm",
                table: "Products",
                column: "SearchName")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_products_searchvector",
                table: "Products",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_products_searchname_trgm",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "ix_products_searchvector",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SearchName",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SearchVector",
                table: "Products");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:unaccent", ",,");

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS immutable_unaccent(text);");
        }
    }
}
