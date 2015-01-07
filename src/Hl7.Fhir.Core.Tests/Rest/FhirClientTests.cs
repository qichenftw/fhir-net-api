﻿/* 
 * Copyright (c) 2014, Furore (info@furore.com) and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.githubusercontent.com/ewoutkramer/fhir-net-api/master/LICENSE
 */

using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Xml;
using System.Net;
using System.IO;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Model;
using Hl7.Fhir.Support;
using Hl7.Fhir.Search;
using System.Threading.Tasks;

namespace Hl7.Fhir.Tests
{
    [TestClass]
#if PORTABLE45
	public class PortableFhirClientTests
#else
    public class FhirClientTests
#endif
    {
        // Uri testEndpoint = new Uri("http://spark.furore.com/fhir");
        // Uri testEndpoint = new Uri("http://localhost.fiddler:1396/fhir");
        // Uri testEndpoint = new Uri("http://localhost:1396/fhir");
        Uri testEndpoint = new Uri("http://fhir-dev.healthintersections.com.au/open");
        // Uri testEndpoint = new Uri("https://api.fhir.me");

        [TestInitialize]
        public void TestInitialize()
        {
            System.Diagnostics.Trace.WriteLine("Testing against fhir server: " + testEndpoint.OriginalString);
        }

        [TestMethod, TestCategory("FhirClient")]
        public void FetchConformance()
        {
            FhirClient client = new FhirClient(testEndpoint);

            var entry = client.Conformance();

            Assert.IsNotNull(entry);
            // Assert.AreEqual("Spark.Service", c.Software.Name); // This is only for ewout's server
            Assert.AreEqual(Conformance.RestfulConformanceMode.Server, entry.Rest[0].Mode.Value);
            Assert.AreEqual(HttpStatusCode.OK, client.LastResponseDetails.Result);

            // Does't currently work on Grahame's server
            Assert.AreEqual(ContentType.XML_CONTENT_HEADER, client.LastResponseDetails.ContentType.ToLower());
        }


        [TestMethod, TestCategory("FhirClient")]
        public void ReadWithFormat()
        {
            FhirClient client = new FhirClient(testEndpoint);

            client.UseFormatParam = true;
            client.PreferredFormat = ResourceFormat.Json;

            var loc = client.Read<Patient>("Patient/1");

            Assert.AreEqual(ResourceFormat.Json, ContentType.GetResourceFormatFromContentType(client.LastResponseDetails.ContentType));
        }


        [TestMethod, TestCategory("FhirClient")]
        public void Read()
        {
            FhirClient client = new FhirClient(testEndpoint);

            var loc = client.Read<Location>("Location/1");
            Assert.IsNotNull(loc);
            Assert.AreEqual("Den Burg", loc.Address.City);

            Assert.AreEqual("1", loc.Id);
            Assert.IsNotNull(loc.Meta.VersionId);

            var loc2 = client.Read<Location>(ResourceIdentity.Build("Location", "1", loc.Meta.VersionId));
            Assert.IsNotNull(loc2);
            Assert.AreEqual(loc2.Id, loc.Id);
            Assert.AreEqual(loc2.Meta.VersionId, loc.Meta.VersionId);

            try
            {
                var random = client.Read<Location>(new Uri("Location/45qq54", UriKind.Relative));
                Assert.Fail();
            }
            catch (FhirOperationException)
            {
                Assert.IsTrue(client.LastResponseDetails.Result == HttpStatusCode.NotFound);
            }

            var loc3 = client.Read<Location>(ResourceIdentity.Build("Location", "1", loc.Meta.VersionId));
            Assert.IsNotNull(loc3);
            Assert.AreEqual(FhirSerializer.SerializeResourceToJson(loc),
                            FhirSerializer.SerializeResourceToJson(loc3));

            var loc4 = client.Read<Location>(loc.ResourceIdentity());
            Assert.IsNotNull(loc4);
            Assert.AreEqual(FhirSerializer.SerializeResourceToJson(loc),
                            FhirSerializer.SerializeResourceToJson(loc4));
        }


        [TestMethod, TestCategory("FhirClient")]
        public void ReadRelative()
        {
            FhirClient client = new FhirClient(testEndpoint);

            var loc = client.Read<Location>(new Uri("Location/1", UriKind.Relative));
            Assert.IsNotNull(loc);
            Assert.AreEqual("Den Burg", loc.Address.City);

            var ri = ResourceIdentity.Build(testEndpoint, "Location", "1");
            loc = client.Read<Location>(ri);
            Assert.IsNotNull(loc);
            Assert.AreEqual("Den Burg", loc.Address.City);
        }

#if PORTABLE45
		[TestMethod, TestCategory("FhirClient")]
		public void ReadRelativeAsync()
		{
			FhirClient client = new FhirClient(testEndpoint);

			var loc = client.ReadAsync<Location>(new Uri("Location/1", UriKind.Relative)).Result;
			Assert.IsNotNull(loc);
			Assert.AreEqual("Den Burg", loc.Resource.Address.City);

			var ri = ResourceIdentity.Build(testEndpoint, "Location", "1");
			loc = client.ReadAsync<Location>(ri).Result;
			Assert.IsNotNull(loc);
			Assert.AreEqual("Den Burg", loc.Resource.Address.City);
		}
#endif
        [TestMethod, TestCategory("FhirClient")]
        public void Search()
        {
            FhirClient client = new FhirClient(testEndpoint);
            Bundle result;

            result = client.Search<DiagnosticReport>();
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Entry.Count() > 10, "Test should use testdata with more than 10 reports");

            result = client.Search<DiagnosticReport>(pageSize: 10);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Entry.Count <= 10);

            var withSubject =
                result.Entry.ByResourceType<DiagnosticReport>().FirstOrDefault(dr => dr.Subject != null);
            Assert.IsNotNull(withSubject, "Test should use testdata with a report with a subject");

            ResourceIdentity ri = withSubject.ResourceIdentity();

            result = client.SearchById<DiagnosticReport>(ri.Id,
                        includes: new string[] { "DiagnosticReport.subject" });
            Assert.IsNotNull(result);

            Assert.AreEqual(2, result.Entry.Count);  // should have subject too

            Assert.IsNotNull(result.Entry.Single(entry => entry.Resource.ResourceIdentity().ResourceType ==
                        typeof(DiagnosticReport).GetCollectionName()));
            Assert.IsNotNull(result.Entry.Single(entry => entry.Resource.ResourceIdentity().ResourceType ==
                        typeof(Patient).GetCollectionName()));

            result = client.Search<Patient>(new string[] { "name=Everywoman", "name=Eve" });

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Entry.Count > 0);
        }

#if PORTABLE45
        [TestMethod, TestCategory("FhirClient")]
        public void SearchAsync()
        {
            FhirClient client = new FhirClient(testEndpoint);
            Bundle result;

            result = client.SearchAsync<DiagnosticReport>().Result;
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Entry.Count() > 10, "Test should use testdata with more than 10 reports");

            result = client.SearchAsync<DiagnosticReport>(pageSize: 10).Result;
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Entry.Count <= 10);

            var withSubject = 
                result.Entry.ByResourceType<DiagnosticReport>().FirstOrDefault(dr => dr.Resource.Subject != null);
            Assert.IsNotNull(withSubject, "Test should use testdata with a report with a subject");

            ResourceIdentity ri = new ResourceIdentity(withSubject.Id);

            result = client.SearchByIdAsync<DiagnosticReport>(ri.Id, 
                        includes: new string[] { "DiagnosticReport.subject" }).Result;
            Assert.IsNotNull(result);

            Assert.AreEqual(2, result.Entry.Count);  // should have subject too

            Assert.IsNotNull(result.Entry.Single(entry => new ResourceIdentity(entry.Id).Collection ==
                        typeof(DiagnosticReport).GetCollectionName()));
            Assert.IsNotNull(result.Entry.Single(entry => new ResourceIdentity(entry.Id).Collection ==
                        typeof(Patient).GetCollectionName()));

            result = client.SearchAsync<Patient>(new string[] { "name=Everywoman", "name=Eve" }).Result;

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Entry.Count > 0);
        }
#endif

        [TestMethod, TestCategory("FhirClient")]
        public void Paging()
        {
            FhirClient client = new FhirClient(testEndpoint);

            var result = client.Search<DiagnosticReport>(pageSize: 10);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Entry.Count <= 10);

            var firstId = result.Entry.First().Resource.Id;

            // Browse forward
            result = client.Continue(result);
            Assert.IsNotNull(result);
            var nextId = result.Entry.First().Resource.Id;
            Assert.AreNotEqual(firstId, nextId);

            // Browse to first
            result = client.Continue(result, PageDirection.First);
            Assert.IsNotNull(result);
            var prevId = result.Entry.First().Resource.Id;
            Assert.AreEqual(firstId, prevId);

            // Forward, then backwards
            result = client.Continue(result, PageDirection.Next);
            Assert.IsNotNull(result);
            result = client.Continue(result, PageDirection.Previous);
            Assert.IsNotNull(result);
            prevId = result.Entry.First().Resource.Id;
            Assert.AreEqual(firstId, prevId);
        }

        [TestMethod, TestCategory("FhirClient")]
        public void PagingInJson()
        {
            FhirClient client = new FhirClient(testEndpoint);
            client.PreferredFormat = ResourceFormat.Json;

            var result = client.Search<DiagnosticReport>(pageSize: 10);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Entry.Count <= 10);

            var firstId = result.Entry.First().Resource.Id;

            // Browse forward
            result = client.Continue(result);
            Assert.IsNotNull(result);
            var nextId = result.Entry.First().Resource.Id;
            Assert.AreNotEqual(firstId, nextId);

            // Browse to first
            result = client.Continue(result, PageDirection.First);
            Assert.IsNotNull(result);
            var prevId = result.Entry.First().Resource.Id;
            Assert.AreEqual(firstId, prevId);

            // Forward, then backwards
            result = client.Continue(result, PageDirection.Next);
            Assert.IsNotNull(result);
            result = client.Continue(result, PageDirection.Previous);
            Assert.IsNotNull(result);
            prevId = result.Entry.First().Resource.Id;
            Assert.AreEqual(firstId, prevId);
        }


        private Uri createdTestPatientUrl = null;

        /// <summary>
        /// This test is also used as a "setup" test for the History test.
        /// If you change the number of operations in here, this will make the History test fail.
        /// </summary>
        [TestMethod, TestCategory("FhirClient")]
        public void CreateEditDelete()
        {
            var pat = (Patient)FhirParser.ParseResourceFromXml(File.ReadAllText(@"TestData\TestPatient.xml"));
            var key = new Random().Next();
            pat.Id = "NetApiCRUDTestPatient" + key;

            FhirClient client = new FhirClient(testEndpoint);

            var fe = client.Create(pat);
            Assert.IsNotNull(fe);
            Assert.IsNotNull(fe.Id);
            Assert.IsNotNull(fe.Meta.VersionId);
            createdTestPatientUrl = fe.ResourceIdentity();

            fe.Identifier.Add(new Identifier("http://hl7.org/test/2", "3141592"));
            var fe2 = client.Update(fe);

            Assert.IsNotNull(fe2);
            Assert.AreEqual(fe.Id, fe2.Id);
            Assert.AreNotEqual(fe.ResourceIdentity(), fe2.ResourceIdentity());
            Assert.AreEqual(2, fe2.Identifier.Count);

            fe.Identifier.Add(new Identifier("http://hl7.org/test/3", "3141592"));
            var fe3 = client.Update(fe);
            Assert.IsNotNull(fe3);
            Assert.AreEqual(3, fe3.Identifier.Count);

            client.Delete(fe3);

            try
            {
                // Get most recent version
                fe = client.Read<Patient>(fe.ResourceIdentity().WithoutVersion());
                Assert.Fail();
            }
            catch
            {
                Assert.IsTrue(client.LastResponseDetails.Result == HttpStatusCode.Gone);
            }
        }

#if PORTABLE45
		/// <summary>
		/// This test is also used as a "setup" test for the History test.
		/// If you change the number of operations in here, this will make the History test fail.
		/// </summary>
		[TestMethod, TestCategory("FhirClient")]
		public void CreateEditDeleteAsync()
		{
			var furore = new Organization
			{
				Name = "Furore",
				Identifier = new List<Identifier> { new Identifier("http://hl7.org/test/1", "3141") },
				Telecom = new List<Contact> { new Contact { System = Contact.ContactSystem.Phone, Value = "+31-20-3467171" } }
			};

			FhirClient client = new FhirClient(testEndpoint);
			var tags = new List<Tag> { new Tag("http://nu.nl/testname", Tag.FHIRTAGSCHEME_GENERAL, "TestCreateEditDelete") };

			var fe = client.CreateAsync<Organization>(furore, tags: tags, refresh: true).Result;

			Assert.IsNotNull(furore);
			Assert.IsNotNull(fe);
			Assert.IsNotNull(fe.Id);
			Assert.IsNotNull(fe.SelfLink);
			Assert.AreNotEqual(fe.Id, fe.SelfLink);
			Assert.IsNotNull(fe.Tags);
			Assert.AreEqual(1, fe.Tags.Count(), "Tag count on new organization record don't match");
			Assert.AreEqual(fe.Tags.First(), tags[0]);
			createdTestOrganizationUrl = fe.Id;

			fe.Resource.Identifier.Add(new Identifier("http://hl7.org/test/2", "3141592"));
			var fe2 = client.UpdateAsync(fe, refresh: true).Result;

			Assert.IsNotNull(fe2);
			Assert.AreEqual(fe.Id, fe2.Id);
			Assert.AreNotEqual(fe.SelfLink, fe2.SelfLink);
			Assert.AreEqual(2, fe2.Resource.Identifier.Count);

			Assert.IsNotNull(fe2.Tags);
			Assert.AreEqual(1, fe2.Tags.Count(), "Tag count on updated organization record don't match");
			Assert.AreEqual(fe2.Tags.First(), tags[0]);

			fe.Resource.Identifier.Add(new Identifier("http://hl7.org/test/3", "3141592"));
			var fe3 = client.UpdateAsync(fe2.Id, fe.Resource, refresh: true).Result;
			Assert.IsNotNull(fe3);
			Assert.AreEqual(3, fe3.Resource.Identifier.Count);

			client.DeleteAsync(fe3).Wait();

			try
			{
				// Get most recent version
				fe = client.ReadAsync<Organization>(new ResourceIdentity(fe.Id)).Result;
				Assert.Fail();
			}
			catch
			{
				Assert.IsTrue(client.LastResponseDetails.Result == HttpStatusCode.Gone);
			}
		}
#endif

        [TestMethod, TestCategory("FhirClient")]
        public void History()
        {
            DateTimeOffset timestampBeforeCreationAndDeletions = DateTimeOffset.Now;

            CreateEditDelete(); // this test does a create, update, update, delete (4 operations)

            FhirClient client = new FhirClient(testEndpoint);
            Bundle history = client.History(createdTestPatientUrl);
            Assert.IsNotNull(history);
            Assert.AreEqual(4, history.Entry.Count());
            Assert.AreEqual(3, history.Entry.Where(entry => entry.Resource != null).Count());
            Assert.AreEqual(1, history.Entry.Where(entry => entry.Deleted != null).Count());

            // Now, assume no one is quick enough to insert something between now and the next
            // tests....

            history = client.TypeHistory<Patient>(timestampBeforeCreationAndDeletions);
            Assert.IsNotNull(history);
            Assert.AreEqual(4, history.Entry.Count());
            Assert.AreEqual(3, history.Entry.Where(entry => entry.Resource != null).Count());
            Assert.AreEqual(1, history.Entry.Where(entry => entry.Deleted != null).Count());

            //EK: Our server can't yet do this
            //history = client.WholeSystemHistory(now);
            //Assert.IsNotNull(history);
            //Assert.AreEqual(3, history.Entry.Count());
            //Assert.AreEqual(2, history.Entry.Where(entry => entry.Resource != null).Count());
            //Assert.AreEqual(1, history.Entry.Where(entry => entry.Deleted != null).Count());
        }


        [TestMethod, TestCategory("FhirClient")]
        public void ManipulateMeta()
        {
            FhirClient client = new FhirClient(testEndpoint);
            var pat = FhirParser.ParseResourceFromXml(File.ReadAllText(@"TestData\TestPatient.xml"));
            var key = new Random().Next();
            pat.Id = "NetApiMetaTestPatient" + key;

            var meta = new Resource.ResourceMetaComponent();
            meta.ProfileElement.Add(new FhirUri("http://someserver.org/fhir/Profile/XYZ1-" + key));
            meta.Security.Add(new Coding("http://mysystem.com/sec", "1234-" + key));
            meta.Tag.Add(new Coding("http://mysystem.com/tag", "sometag1-" + key));

            pat.Meta = meta;

            // First, create a patient with the first set of meta
            var pat2 = client.Create(pat);
          //  var loc = pat2.GetResourceLocation(testEndpoint).WithoutVersion();
            var loc = pat2.GetResourceLocation(testEndpoint);

            // Meta should be present on created patient
            verifyMeta(pat2.Meta, false,key);

            // Should be present when doing instance Meta()
            var meta2 = client.Meta(loc);
            verifyMeta(meta2, false,key);

            // Should be present when doing type Meta()
            meta2 = client.TypeMeta<Patient>();
            verifyMeta(meta2, false,key);

            // Should be present when doing System Meta()
            meta2 = client.WholeSystemMeta();
            verifyMeta(meta2, false,key);

            // Now add some additional meta to the patient
            var newMeta = new Resource.ResourceMetaComponent();
            newMeta.ProfileElement.Add(new FhirUri("http://someserver.org/fhir/Profile/XYZ2-" + key));
            newMeta.Security.Add(new Coding("http://mysystem.com/sec", "5678-" + key));
            newMeta.Tag.Add(new Coding("http://mysystem.com/tag", "sometag2-" + key));

            client.AffixMeta(loc, newMeta);
            var pat3 = client.Read<Patient>(loc);

            // New and old meta should be present on instance
            verifyMeta(pat3.Meta, true, key);

            // New and old meta should be present on Meta()
            var newMeta2 = client.Meta(loc);
            verifyMeta(newMeta2, true, key);

            // New and old meta should be present when doing type Meta()
            newMeta2 = client.TypeMeta<Patient>();
            verifyMeta(newMeta2, true, key);

            // New and old meta should be present when doing system Meta()
            newMeta2 = client.WholeSystemMeta();
            verifyMeta(newMeta2, true, key);

            // Now, remove those new meta tags
            client.DeleteMeta(loc, newMeta);

            // Should no longer be present on instance
            var pat4 = client.Read<Patient>(loc);
            verifyMeta(pat4.Meta, false, key);

            // Should no longer be present when doing instance Meta()
            meta2 = client.Meta(loc);
            verifyMeta(meta2, false, key);

            // Should no longer be present when doing type Meta()
            meta2 = client.TypeMeta<Patient>();
            verifyMeta(meta2, false, key);

            // Should no longer be present when doing System Meta()
            meta2 = client.WholeSystemMeta();
            verifyMeta(meta2, false, key);
        }


        private void verifyMeta(Resource.ResourceMetaComponent meta, bool hasNew, int key)
        {
            Assert.IsTrue(meta.Profile.Contains("http://someserver.org/fhir/Profile/XYZ1-" + key));
            Assert.IsTrue(meta.Security.Select(c => c.Code + "@" + c.System).Contains("1234-" + key + "@http://mysystem.com/sec"));
            Assert.IsTrue(meta.Tag.Select(c => c.Code + "@" + c.System).Contains("sometag1-" + key + "@http://mysystem.com/tag"));

            if (hasNew)
            {
                Assert.IsTrue(meta.Profile.Contains("http://someserver.org/fhir/Profile/XYZ2-" + key));
                Assert.IsTrue(meta.Security.Select(c => c.Code + "@" + c.System).Contains("5678-" + key + "@http://mysystem.com/sec"));
                Assert.IsTrue(meta.Tag.Select(c => c.Code + "@" + c.System).Contains("sometag2-" + key + "@http://mysystem.com/tag"));
            }

            if (!hasNew)
            {
                Assert.IsFalse(meta.Profile.Contains("http://someserver.org/fhir/Profile/XYZ2-" + key));
                Assert.IsFalse(meta.Security.Select(c => c.Code + "@" + c.System).Contains("5678-" + key + "@http://mysystem.com/sec"));
                Assert.IsFalse(meta.Tag.Select(c => c.Code + "@" + c.System).Contains("sometag2-" + key + "@http://mysystem.com/tag"));
            }
        }


        [TestMethod]
        public void CreateDynamic()
        {
            Resource furore = new Organization
            {
                Name = "Furore",
                Identifier = new List<Identifier> { new Identifier("http://hl7.org/test/1", "3141") },
                Telecom = new List<ContactPoint> { 
                    new ContactPoint { System = ContactPoint.ContactPointSystem.Phone, Value = "+31-20-3467171", Use = ContactPoint.ContactPointUse.Work },
                    new ContactPoint { System = ContactPoint.ContactPointSystem.Fax, Value = "+31-20-3467172" } 
                }
            };

            FhirClient client = new FhirClient(testEndpoint);

            var fe = client.Create(furore);
            Assert.IsNotNull(fe);
        }
    }
}