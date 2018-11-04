using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.DotNet.VersionTools.BuildManifest.Model;
using TrackerGen.Data;

namespace TrackerGen
{
    public class BuildXmlLoader
    {
        public static OrchestratedBuild Load(string xmlText, string branch)
        {
            var model = OrchestratedBuildModel.Parse(XDocument.Parse(xmlText).Root);

            var orchestratedBuild = new OrchestratedBuild
            {
                BuildNumber = model.Identity.BuildId,
                Branch = branch,
                OrchestratedBuildId = $"{branch}/{model.Identity.BuildId}",
                Name = model.Identity.Name,
                IsStable = string.IsNullOrEmpty(model.Identity.IsStable) ? false : bool.Parse(model.Identity.IsStable),
                VersionStamp = model.Identity.VersionStamp
            };

            var buildsIndex = new Dictionary<string, Build>();
            foreach (var buildModel in model.Builds.Where(b => b.Name != "anonymous"))
            {
                var build = new Build
                {
                    OrchestratedBuildId = orchestratedBuild.OrchestratedBuildId,
                    Name = buildModel.Name,
                    BuildNumber = buildModel.BuildId,
                    BuildId = $"{orchestratedBuild.OrchestratedBuildId}/builds/{buildModel.Name}/{buildModel.BuildId}",
                    Branch = buildModel.Branch,
                    ProductVersion = buildModel.ProductVersion,
                    Commit = buildModel.Commit,
                };

                buildsIndex[build.Name] = build;
                orchestratedBuild.Builds.Add(build);
            }

            var endpointIndex = new Dictionary<string, Endpoint>();
            var packageIndex = new Dictionary<string, Package>();
            var blobIndex = new Dictionary<string, Blob>();
            foreach (var endpoint in model.Endpoints)
            {
                var baseUrl = endpoint.Url;
                if (baseUrl.EndsWith("/index.json"))
                {
                    baseUrl = baseUrl.Substring(0, baseUrl.Length - 11);
                }
                if (endpoint.IsOrchestratedBlobFeed)
                {
                    baseUrl += "/assets";
                }

                var ep = new Endpoint
                {
                    EndpointId = $"{orchestratedBuild.OrchestratedBuildId}/endpoints/{endpoint.Id}",
                    Id = endpoint.Id,
                    Type = endpoint.Type,
                    Url = endpoint.Url
                };

                endpointIndex[ep.EndpointId] = ep;
                orchestratedBuild.Endpoints.Add(ep);

                foreach (var artifact in endpoint.Artifacts.Packages)
                {
                    if (packageIndex.TryGetValue(artifact.Id, out var existingRef))
                    {
                        existingRef.Endpoints.Add(new EndpointRef()
                        {
                            EndpointRefId = $"{orchestratedBuild.OrchestratedBuildId}/endpoints/{ep.EndpointId}/packages/{artifact.Id}",
                            EndpointId = ep.EndpointId,
                            ArtifactUrl = $"{baseUrl}/flatcontainer/{artifact.Id}/{artifact.Version}/{artifact.Id}.{artifact.Version}.nupkg",
                        });
                    }
                    else
                    {
                        var packageRef = new Package
                        {
                            PackageId = $"{orchestratedBuild.OrchestratedBuildId}/packages/{artifact.Id}",
                            Id = artifact.Id,
                            Version = artifact.Version,
                            NonShipping = bool.Parse(artifact.Attributes.TryGetValue("NonShipping", bool.TrueString))
                        };
                        if (!string.IsNullOrEmpty(artifact.OriginBuildName) && buildsIndex.TryGetValue(artifact.OriginBuildName, out var build))
                        {
                            packageRef.OriginBuildId = build.BuildId;
                        }
                        packageIndex[artifact.Id] = packageRef;
                        orchestratedBuild.Packages.Add(packageRef);
                    }
                }

                foreach (var artifact in endpoint.Artifacts.Blobs)
                {
                    if (blobIndex.TryGetValue(artifact.Id, out var existingRef))
                    {
                        existingRef.Endpoints.Add(new EndpointRef()
                        {
                            EndpointRefId = $"{orchestratedBuild.OrchestratedBuildId}/endpoints/{ep.EndpointId}/blobs/{artifact.Id}",
                            EndpointId = ep.EndpointId,
                            ArtifactUrl = $"{baseUrl}/assets/{artifact.Id}",
                        });
                    }
                    else
                    {
                        var blobRef = new Blob
                        {
                            BlobId = $"{orchestratedBuild.OrchestratedBuildId}/blobs/{artifact.Id}",
                            Id = artifact.Id,
                            Type = artifact.Attributes.TryGetValue("Type"),
                            ShipInstaller = artifact.Attributes.TryGetValue("ShipInstaller")
                        };
                        blobIndex[artifact.Id] = blobRef;
                        orchestratedBuild.Blobs.Add(blobRef);
                    }
                }
            }

            return orchestratedBuild;
        }
    }
}
