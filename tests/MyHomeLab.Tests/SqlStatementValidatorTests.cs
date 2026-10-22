using MyHomeLab.Application.Errors;
using MyHomeLab.Application.Services;
using Xunit;

namespace MyHomeLab.Tests;

public class SqlStatementValidatorTests
{
    [Theory]
    [InlineData("SELECT 1", "SELECT")]
    [InlineData("select * from apps", "SELECT")]
    [InlineData("INSERT INTO apps (name) VALUES ('x')", "INSERT")]
    [InlineData("UPDATE apps SET name = 'y' WHERE id = '00000000-0000-0000-0000-000000000000'", "UPDATE")]
    [InlineData("DELETE FROM apps WHERE id = '00000000-0000-0000-0000-000000000000'", "DELETE")]
    [InlineData("WITH t AS (SELECT 1) SELECT * FROM t", "SELECT")]
    [InlineData("WITH t AS (DELETE FROM apps RETURNING *) INSERT INTO logs SELECT * FROM t", "INSERT")]
    [InlineData("WITH t AS (INSERT INTO logs (name) VALUES ('x') RETURNING *) SELECT * FROM t", "SELECT")]
    [InlineData("-- note\nSELECT 1", "SELECT")]
    [InlineData("/* note */ SELECT 1", "SELECT")]
    [InlineData("SELECT 1;", "SELECT")]
    [InlineData("SELECT 1; ;", "SELECT")]
    [InlineData("SELECT ';' AS s", "SELECT")]
    [InlineData("SELECT 1 -- ; DROP TABLE apps", "SELECT")]
    [InlineData("SELECT $$; $$ AS s", "SELECT")]
    [InlineData("SELECT 'it''s'", "SELECT")]
    [InlineData("SELECT \"INTO\" FROM apps", "SELECT")]
    [InlineData("(SELECT 1)", "SELECT")]
    [InlineData("UPDATE apps SET name = e'a\\'b'", "UPDATE")]
    public void Validate_AllowsDml(string sql, string expectedCommand)
    {
        Assert.Equal(expectedCommand, SqlStatementValidator.Validate(sql));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-- only a comment")]
    [InlineData("/* nothing */")]
    [InlineData("CREATE TABLE t (id int)")]
    [InlineData("ALTER TABLE apps ADD COLUMN x int")]
    [InlineData("DROP TABLE apps")]
    [InlineData("TRUNCATE apps")]
    [InlineData("COPY apps TO STDOUT")]
    [InlineData("DO $$ BEGIN END $$")]
    [InlineData("EXPLAIN SELECT 1")]
    [InlineData("GRANT SELECT ON apps TO joe")]
    [InlineData("SET search_path TO public")]
    [InlineData("SELECT 1; SELECT 2")]
    [InlineData("SELECT 1; DROP TABLE apps")]
    [InlineData("SELECT 1;; DROP TABLE apps")]
    [InlineData("SELECT E'\\''; DROP TABLE apps; --")]
    [InlineData("SELECT * INTO t FROM apps")]
    [InlineData("(SELECT * INTO t FROM apps)")]
    [InlineData("WITH x AS (SELECT 1) SELECT * INTO t FROM x")]
    [InlineData("WITH x AS (SELECT 1) VALUES (1)")]
    [InlineData("SELECT 'abc")]
    [InlineData("SELECT $tag$ abc")]
    [InlineData("SELECT \"unterminated")]
    public void Validate_RejectsEverythingElse(string? sql)
    {
        Assert.Throws<ValidationException>(() => SqlStatementValidator.Validate(sql));
    }

    [Fact]
    public void Validate_TooLong_Rejects()
    {
        var sql = "SELECT '" + new string('x', 100_000) + "'";
        Assert.Throws<ValidationException>(() => SqlStatementValidator.Validate(sql));
    }

    [Fact]
    public void Validate_MultipleStatements_ExplainsSingleStatementRule()
    {
        var ex = Assert.Throws<ValidationException>(() => SqlStatementValidator.Validate("SELECT 1; SELECT 2"));
        Assert.Contains("one statement", ex.Message);
    }

    [Fact]
    public void Validate_SelectInto_PointsAtInsertInto()
    {
        var ex = Assert.Throws<ValidationException>(() => SqlStatementValidator.Validate("SELECT * INTO t FROM apps"));
        Assert.Contains("INSERT INTO", ex.Message);
    }

    [Fact]
    public void Validate_Ddl_ListsAllowedCommands()
    {
        var ex = Assert.Throws<ValidationException>(() => SqlStatementValidator.Validate("DROP TABLE apps"));
        Assert.Contains("SELECT", ex.Message);
        Assert.Contains("DELETE", ex.Message);
    }

    [Fact]
    public void Validate_WithMerge_Rejects()
    {
        var ex = Assert.Throws<ValidationException>(() =>
            SqlStatementValidator.Validate("WITH t AS (SELECT 1) MERGE INTO apps USING t ON false WHEN NOT MATCHED THEN INSERT VALUES (1)"));
        Assert.Contains("WITH", ex.Message);
    }
}
