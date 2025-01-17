﻿using Kusto.Language.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DeltaKustoLib.CommandModel.Policies
{
    /// <summary>
    /// Models <see cref="https://docs.microsoft.com/en-us/azure/data-explorer/kusto/management/batching-policy#altering-the-ingestionbatching-policy"/>
    /// </summary>
    [Command(12100, "Alter Ingestion Batching Policies")]
    public class AlterIngestionBatchingPolicyCommand : EntityPolicyCommandBase
    {
        public override string CommandFriendlyName => ".alter <entity> policy ingestionbatching";

        public override string ScriptPath => EntityType == EntityType.Database
            ? $"tables/policies/ingestionbatching/create/{EntityName}"
            : $"databases/policies/ingestionbatching/create";

        public AlterIngestionBatchingPolicyCommand(
            EntityType entityType,
            EntityName entityName,
            JsonDocument policy) : base(entityType, entityName, policy)
        {
        }

        public AlterIngestionBatchingPolicyCommand(
            EntityType entityType,
            EntityName entityName,
            TimeSpan maximumBatchingTimeSpan,
            int maximumNumberOfItems,
            int maximumRawDataSizeMb)
            : this(
                  entityType,
                  entityName,
                  ToJsonDocument(new
                  {
                      MaximumBatchingTimeSpan = maximumBatchingTimeSpan.ToString(),
                      MaximumNumberOfItems = maximumNumberOfItems,
                      MaximumRawDataSizeMb = maximumRawDataSizeMb
                  }))
        {
        }

        internal static CommandBase FromCode(SyntaxElement rootElement)
        {
            var entityKinds = rootElement
                .GetDescendants<SyntaxElement>(s => s.Kind == SyntaxKind.TableKeyword
                || s.Kind == SyntaxKind.DatabaseKeyword)
                .Select(s => s.Kind);

            if (!entityKinds.Any())
            {
                throw new DeltaException("Alter ingestion batching policy requires to act on a table or database (cluster isn't supported)");
            }
            var entityKind = entityKinds.First();
            var entityType = entityKind == SyntaxKind.TableKeyword
                ? EntityType.Table
                : EntityType.Database;
            var entityName = rootElement.GetDescendants<NameReference>().Last();
            var policyText = QuotedText.FromLiteral(
                rootElement.GetUniqueDescendant<LiteralExpression>(
                    "IngestionBatching",
                    e => e.NameInParent == "IngestionBatchingPolicy"));
            var policy = Deserialize<JsonDocument>(policyText.Text);

            if (policy == null)
            {
                throw new DeltaException(
                    $"Can't extract policy objects from {policyText.ToScript()}");
            }

            return new AlterIngestionBatchingPolicyCommand(
                entityType,
                EntityName.FromCode(entityName.Name),
                policy);
        }

        public override string ToScript(ScriptingContext? context)
        {
            var builder = new StringBuilder();

            builder.Append(".alter ");
            builder.Append(EntityType == EntityType.Table ? "table" : "database");
            builder.Append(" ");
            if (EntityType == EntityType.Database && context?.CurrentDatabaseName != null)
            {
                builder.Append(context.CurrentDatabaseName.ToScript());
            }
            else
            {
                builder.Append(EntityName.ToScript());
            }
            builder.Append(" policy ingestionbatching");
            builder.AppendLine();
            builder.Append("```");
            builder.Append(SerializePolicy());
            builder.AppendLine();
            builder.Append("```");

            return builder.ToString();
        }

        internal static IEnumerable<CommandBase> ComputeDelta(
            AlterIngestionBatchingPolicyCommand? currentCommand,
            AlterIngestionBatchingPolicyCommand? targetCommand)
        {
            var hasCurrent = currentCommand != null;
            var hasTarget = targetCommand != null;

            if (hasCurrent && !hasTarget)
            {   //  No target, we remove the current policy
                yield return new DeleteIngestionBatchingPolicyCommand(
                    currentCommand!.EntityType,
                    currentCommand!.EntityName);
            }
            else if (hasTarget)
            {
                if (!hasCurrent || !currentCommand!.Equals(targetCommand!))
                {   //  There is a target and either no current or the current is different
                    yield return targetCommand!;
                }
            }
            else
            {   //  Both target and current are null:  no delta
            }
        }
    }
}