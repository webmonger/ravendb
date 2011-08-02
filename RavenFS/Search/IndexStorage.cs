using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Runtime.CompilerServices;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Version = Lucene.Net.Util.Version;

namespace RavenFS.Search
{
	public class IndexStorage : IDisposable
	{
		private readonly string path;
		private FSDirectory directory;
		private LowerCaseKeywordAnalyzer analyzer;
		private IndexWriter writer;
		private readonly object writerLock = new object();
		private IndexSearcher searcher;

		public IndexStorage(string path, NameValueCollection _)
		{
			if (Path.IsPathRooted(path) == false)
				path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
			this.path = Path.Combine(path, "Index.ravenfs");
		}

		public void Initialize()
		{
			directory = FSDirectory.Open(new DirectoryInfo(path));
			if (IndexWriter.IsLocked(directory))
				IndexWriter.Unlock(directory);

			analyzer = new LowerCaseKeywordAnalyzer();
			writer = new IndexWriter(directory, analyzer, IndexWriter.MaxFieldLength.UNLIMITED);
			searcher = new IndexSearcher(writer.GetReader());
		}

		public string[] Query(string query, int start, int pageSize)
		{
			var queryParser = new QueryParser(Version.LUCENE_29, "", analyzer);
			var q = queryParser.Parse(query);

			var topDocs = searcher.Search(q, pageSize + start);

			var results = new List<string>();

			for (var i = start; i < pageSize + start && i < topDocs.totalHits; i++)
			{
				var document = searcher.Doc(i);
				results.Add(document.Get("__key"));
			}
			return results.ToArray();
		}

		public void Index(string key, NameValueCollection metadata)
		{
			lock (writerLock)
			{
				var doc = new Document();

				doc.Add(new Field("__key", key, Field.Store.YES, Field.Index.ANALYZED_NO_NORMS));

				foreach (var metadataKey in metadata.AllKeys)
				{
					var values = metadata.GetValues(metadataKey);
					if(values == null)
						continue;

					foreach (var value in values)
					{
						doc.Add(new Field(metadataKey, value, Field.Store.NO, Field.Index.ANALYZED_NO_NORMS));
					}
				}

				writer.DeleteDocuments(new Term("__key", key));
				writer.AddDocument(doc);
				// yes, this is slow, but we aren't expecting high writes count
				writer.Commit();
				ReplaceSearcher();
			}
		}
		public void Dispose()
		{
			analyzer.Close();
			searcher.GetIndexReader().Close();
			searcher.Close();
			writer.Close();
			directory.Close();
		}

		public void Delete(string key)
		{
			lock (writerLock)
			{
				writer.DeleteDocuments(new Term("__key", key));
				writer.Commit();
				ReplaceSearcher();
			}
		}

		private void ReplaceSearcher()
		{
			var currentSearcher = searcher;
			currentSearcher.GetIndexReader().Close();
			currentSearcher.Close();

			searcher = new IndexSearcher(writer.GetReader());
		}
	}
}