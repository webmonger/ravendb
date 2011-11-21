﻿using System.Collections.Specialized;
using System.IO;
using RavenFS.Client;
using Xunit;

namespace RavenFS.Tests.Bugs
{
	public class Queries : ServerTest
	{
		private readonly RavenFileSystemClient client = new RavenFileSystemClient("http://localhost:9090");


		[Fact]
		public void CanQueryMultipleFiles()
		{

			var ms = new MemoryStream();
			var streamWriter = new StreamWriter(ms);
			var expected = new string('a', 1024);
			streamWriter.Write(expected);
			streamWriter.Flush();
			ms.Position = 0;

			client.UploadAsync("abc.txt", new NameValueCollection(), ms).Wait();

			ms.Position = 0;
			client.UploadAsync("CorelVBAManual.PDF", new NameValueCollection
			{
				{"Filename", "CorelVBAManual.PDF"}
			}, ms).Wait();

			ms.Position = 0;
			client.UploadAsync("TortoiseSVN-1.7.0.22068-x64-svn-1.7.0.msi", new NameValueCollection
			{
				{"Filename", "TortoiseSVN-1.7.0.22068-x64-svn-1.7.0.msi"}
			}, ms).Wait();


			var fileInfos = client.SearchAsync("Filename:corelVBAManual.PDF").Result;

			Assert.Equal(1, fileInfos.Length);
			Assert.Equal("CorelVBAManual.PDF", fileInfos[0].Name);
		}

		[Fact]
		public void WillGetOneItemWhenSavingDocumentTwice()
		{

			var ms = new MemoryStream();
			var streamWriter = new StreamWriter(ms);
			var expected = new string('a', 1024);
			streamWriter.Write(expected);
			streamWriter.Flush();
			ms.Position = 0;

			client.UploadAsync("abc.txt", new NameValueCollection(), ms).Wait();

			for (int i = 0; i < 3; i++)
			{
				ms.Position = 0;
				client.UploadAsync("CorelVBAManual.PDF", new NameValueCollection
				{
					{"Filename", "CorelVBAManual.PDF"}
				}, ms).Wait();
			}

			ms.Position = 0;
			client.UploadAsync("TortoiseSVN-1.7.0.22068-x64-svn-1.7.0.msi", new NameValueCollection
			{
				{"Filename", "TortoiseSVN-1.7.0.22068-x64-svn-1.7.0.msi"}
			}, ms).Wait();


			var fileInfos = client.SearchAsync("Filename:corelVBAManual.PDF").Result;

			Assert.Equal(1, fileInfos.Length);
			Assert.Equal("CorelVBAManual.PDF", fileInfos[0].Name);
		}

		[Fact]
		public void ShouldEncodeValues()
		{

			var ms = new MemoryStream();
			var streamWriter = new StreamWriter(ms);
			var expected = new string('a', 1024);
			streamWriter.Write(expected);
			streamWriter.Flush();
			ms.Position = 0;

			const string filename = "10 jQuery Transition Effects- Moving Elements with Style - DevSnippets.txt";
			client.UploadAsync(filename, new NameValueCollection
			{
				{"Item", "10"}
			}, ms).Wait();


			var fileInfos = client.SearchAsync("Item:10*").Result;

			Assert.Equal(1, fileInfos.Length);
			Assert.Equal(filename, fileInfos[0].Name);
		}
	}
}