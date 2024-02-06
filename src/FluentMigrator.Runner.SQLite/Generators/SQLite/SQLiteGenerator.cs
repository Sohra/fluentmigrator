#region License
//
// Copyright (c) 2007-2018, Sean Chambers <schambers80@gmail.com>
// Copyright (c) 2010, Nathan Brown
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//   http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
#endregion

using System.Linq;

using FluentMigrator.Expressions;
using FluentMigrator.Model;
using FluentMigrator.Runner.Generators.Generic;

using JetBrains.Annotations;

using Microsoft.Extensions.Options;

namespace FluentMigrator.Runner.Generators.SQLite
{
    // ReSharper disable once InconsistentNaming
    public class SQLiteGenerator : GenericGenerator
    {
        public SQLiteGenerator()
            : this(new SQLiteQuoter())
        {
        }

        public SQLiteGenerator(
            [NotNull] SQLiteQuoter quoter)
            : this(quoter, new OptionsWrapper<GeneratorOptions>(new GeneratorOptions()))
        {
        }

        public SQLiteGenerator(
            [NotNull] SQLiteQuoter quoter,
            [NotNull] IOptions<GeneratorOptions> generatorOptions)
            : base(new SQLiteColumn(quoter), quoter, new EmptyDescriptionGenerator(), generatorOptions)
        {
            CompatibilityMode = generatorOptions.Value.CompatibilityMode ?? CompatibilityMode.STRICT;
        }

        public override string RenameTable { get { return "ALTER TABLE {0} RENAME TO {1}"; } }

        public override string Generate(AlterColumnExpression expression)
        {
            return CompatibilityMode.HandleCompatibilty("SQLite does not support alter column");
        }

        public override string Generate(AlterDefaultConstraintExpression expression)
        {
            return CompatibilityMode.HandleCompatibilty("SQLite does not support altering of default constraints");
        }

        public override string Generate(CreateForeignKeyExpression expression)
        {
            // If a FK name starts with $$IGNORE$$_ then it means it was handled by the CREATE TABLE
            // routine and we know it's been handled so we should just not bother erroring.
            if (expression.ForeignKey.Name.StartsWith("$$IGNORE$$_"))
                return string.Empty;

            return CompatibilityMode.HandleCompatibilty("Foreign keys are not supported in SQLite");
        }

        public override string Generate(DeleteForeignKeyExpression expression)
        {
            return CompatibilityMode.HandleCompatibilty("Foreign keys are not supported in SQLite");
        }

        public override string Generate(CreateSequenceExpression expression)
        {
            return CompatibilityMode.HandleCompatibilty("Sequences are not supported in SQLite");
        }

        public override string Generate(DeleteSequenceExpression expression)
        {
            return CompatibilityMode.HandleCompatibilty("Sequences are not supported in SQLite");
        }

        public override string Generate(DeleteDefaultConstraintExpression expression)
        {
            return CompatibilityMode.HandleCompatibilty("Default constraints are not supported in SQLite");
        }

        public override string Generate(CreateConstraintExpression expression)
        {
            if (!expression.Constraint.IsUniqueConstraint)
                return CompatibilityMode.HandleCompatibilty("Only UNIQUE constraints are supported in SQLite");

            // Convert the constraint into a UNIQUE index
            var idx = new CreateIndexExpression();
            idx.Index.Name = expression.Constraint.ConstraintName;
            idx.Index.TableName = expression.Constraint.TableName;
            idx.Index.SchemaName = expression.Constraint.SchemaName;
            idx.Index.IsUnique = true;

            foreach (var col in expression.Constraint.Columns)
                idx.Index.Columns.Add(new IndexColumnDefinition { Name = col });

            return Generate(idx);
        }

        public override string Generate(DeleteConstraintExpression expression)
        {
            if (!expression.Constraint.IsUniqueConstraint)
                return CompatibilityMode.HandleCompatibilty("Only UNIQUE constraints are supported in SQLite");

            // Convert the constraint into a drop UNIQUE index
            var idx = new DeleteIndexExpression();
            idx.Index.Name = expression.Constraint.ConstraintName;
            idx.Index.SchemaName = expression.Constraint.SchemaName;

            return Generate(idx);
        }

        public override string Generate(CreateIndexExpression expression)
        {
            // SQLite prefixes the index name, rather than the table name with the schema

            var indexColumns = new string[expression.Index.Columns.Count];
            IndexColumnDefinition columnDef;

            for (var i = 0; i < expression.Index.Columns.Count; i++)
            {
                columnDef = expression.Index.Columns.ElementAt(i);
                if (columnDef.Direction == Direction.Ascending)
                {
                    indexColumns[i] = Quoter.QuoteColumnName(columnDef.Name) + " ASC";
                }
                else
                {
                    indexColumns[i] = Quoter.QuoteColumnName(columnDef.Name) + " DESC";
                }
            }

            return string.Format(CreateIndex
                , GetUniqueString(expression)
                , GetClusterTypeString(expression)
                , Quoter.QuoteIndexName(expression.Index.Name, expression.Index.SchemaName)
                , Quoter.QuoteTableName(expression.Index.TableName)
                , string.Join(", ", indexColumns));
        }

        public override string Generate(DeleteIndexExpression expression)
        {
            // SQLite prefixes the index name, rather than the table name with the schema

            return string.Format(DropIndex, Quoter.QuoteIndexName(expression.Index.Name, expression.Index.SchemaName));
        }
    }
}
