﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Raven.Abstractions.Data;
using Raven.Database.Data;
using Raven.Database.Impl;
using Raven.Database.Server.WebApi.Attributes;
using Raven.Json.Linq;

namespace Raven.Database.Server.Controllers
{
	[RoutePrefix("bulk_docs")]
	public class DocumentsBatchController : RavenApiController
	{
		[HttpPost("")]
		public async Task<HttpResponseMessage> BulkPost()
		{
			var jsonCommandArray = await ReadJsonArrayAsync();

			var transactionInformation = GetRequestTransaction();
			var commands = (from RavenJObject jsonCommand in jsonCommandArray
							select CommandDataFactory.CreateCommand(jsonCommand, transactionInformation))
				.ToArray();

			//TODO: log
			//context.Log(log => log.Debug(() =>
			//{
			//	if (commands.Length > 15) // this is probably an import method, we will input minimal information, to avoid filling up the log
			//	{
			//		return "\tExecuted " + string.Join(", ", commands.GroupBy(x => x.Method).Select(x => string.Format("{0:#,#;;0} {1} operations", x.Count(), x.Key)));
			//	}

			//	var sb = new StringBuilder();
			//	foreach (var commandData in commands)
			//	{
			//		sb.AppendFormat("\t{0} {1}{2}", commandData.Method, commandData.Key, Environment.NewLine);
			//	}
			//	return sb.ToString();
			//}));

			var batchResult = Database.Batch(commands);
			return GetMessageWithObject(batchResult);
		}

		[HttpDelete("{id}")]
		public HttpResponseMessage BulkDelete(string id)
		{
			var databaseBulkOperations = new DatabaseBulkOperations(Database, GetRequestTransaction());
			return OnBulkOperation(databaseBulkOperations.DeleteByIndex, id);			
		}

		[HttpPatch("{id}")]
		public async Task<HttpResponseMessage> BulkPatch(string id)
		{
			var databaseBulkOperations = new DatabaseBulkOperations(Database, GetRequestTransaction());
			var patchRequestJson = await ReadJsonArrayAsync();
			var patchRequests = patchRequestJson.Cast<RavenJObject>().Select(PatchRequest.FromJson).ToArray();
			return OnBulkOperation((index, query, allowStale) =>
				databaseBulkOperations.UpdateByIndex(index, query, patchRequests, allowStale), id);
		}

		[HttpEval("{id}")]
		public async Task<HttpResponseMessage> BulkEval(string id)
		{
			var databaseBulkOperations = new DatabaseBulkOperations(Database, GetRequestTransaction());
			var advPatchRequestJson = await ReadJsonObjectAsync<RavenJObject>();
			var advPatch = ScriptedPatchRequest.FromJson(advPatchRequestJson);
			return OnBulkOperation((index, query, allowStale) =>
				databaseBulkOperations.UpdateByIndex(index, query, advPatch, allowStale), id);
		}

		private HttpResponseMessage OnBulkOperation(Func<string, IndexQuery, bool, RavenJArray> batchOperation, string index)
		{
			if (string.IsNullOrEmpty(index))
				return new HttpResponseMessage(HttpStatusCode.BadRequest);

			var allowStale = GetAllowStale();
			var indexQuery = GetIndexQuery(maxPageSize: int.MaxValue);

			var status = new BulkOperationStatus();
			var sp = Stopwatch.StartNew();
			long id = 0;

			var task = Task.Factory.StartNew(() =>
			{
				var array = batchOperation(index, indexQuery, allowStale);
				status.State = array;
				status.Completed = true;

				//TODO: log
				//context.Log(log => log.Debug("\tBatch Operation worked on {0:#,#;;0} documents in {1}, task #: {2}", array.Length, sp.Elapsed, id));
			});

			Database.AddTask(task, status, out id);

			return GetMessageWithObject(new {OperationId = id});
		}

		public class BulkOperationStatus
		{
			public RavenJArray State { get; set; }
			public bool Completed { get; set; }
		}
	}
}
