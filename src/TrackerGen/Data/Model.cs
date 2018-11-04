using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace TrackerGen.Data
{
    public class OrchestratedBuild
    {
        [Key]
        public string OrchestratedBuildId { get; set; }
        public string BuildNumber { get; set; }
        public string Branch { get; set; }
        public string Name { get; set; }
        public bool IsStable { get; set; }
        public string VersionStamp { get; set; }

        public List<Build> Builds { get; set; } = new List<Build>();
        public List<Endpoint> Endpoints { get; set; } = new List<Endpoint>();
        public List<Package> Packages { get; set; } = new List<Package>();
        public List<Blob> Blobs { get; set; } = new List<Blob>();
    }

    public class Build
    {
        [Key]
        public string BuildId { get; set; }
        public string BuildNumber { get; set; }
        public string OrchestratedBuildId { get; set; }
        public string Name { get; set; }
        public string ProductVersion { get; set; }
        public string Branch { get; set; }
        public string Commit { get; set; }

        public OrchestratedBuild OrchestratedBuild { get; set; }
    }

    [Owned]
    public class Endpoint
    {
        [Key]
        public string EndpointId { get; set; }
        public string Id { get; set; }
        public string Type { get; set; }
        public string Url { get; set; }
    }

    [Owned]
    public class Blob
    {
        [Key]
        public string BlobId { get; set; }
        public string Id { get; set; }
        public string Type { get; set; }
        public string ShipInstaller { get; set; }
        public bool NonShipping { get; set; }

        public List<EndpointRef> Endpoints { get; set; } = new List<EndpointRef>();
    }

    [Owned]
    public class Package
    {
        [Key]
        public string PackageId { get; set; }
        public string Id { get; set; }
        public string Version { get; set; }
        public bool NonShipping { get; set; }
        public string OriginBuildId { get; set; }

        public List<EndpointRef> Endpoints { get; set; } = new List<EndpointRef>();
    }

    [Owned]
    public class EndpointRef
    {
        [Key]
        public string EndpointRefId { get; set; }
        public string EndpointId { get; set; }
        public string ArtifactUrl { get; set; }
    }
}
