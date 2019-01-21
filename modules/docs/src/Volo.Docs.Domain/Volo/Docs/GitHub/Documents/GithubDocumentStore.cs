using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Octokit;
using Octokit.Internal;
using Volo.Abp.Domain.Services;
using Volo.Docs.Documents;
using Volo.Docs.GitHub.Projects;
using Volo.Docs.Projects;
using Newtonsoft.Json.Linq;
using ProductHeaderValue = Octokit.ProductHeaderValue;
using Project = Volo.Docs.Projects.Project;

namespace Volo.Docs.GitHub.Documents
{
    //TODO: Needs more refactoring

    public class GithubDocumentStore : DomainService, IDocumentStore
    {
        public const string Type = "GitHub";

        public virtual async Task<Document> GetDocument(Project project, string documentName, string version)
        {
            var token = project.GetGitHubAccessTokenOrNull();
            var rootUrl = project.GetGitHubUrl(version);
            var rawRootUrl = CalculateRawRootUrl(rootUrl);
            var rawDocumentUrl = rawRootUrl + documentName;
            var commitHistoryUrl = project.GetGitHubUrlForCommitHistory() + documentName;
            var editLink = rootUrl.ReplaceFirst("/tree/", "/blob/") + documentName;
            var localDirectory = "";
            var fileName = documentName;

            if (documentName.Contains("/"))
            {
                localDirectory = documentName.Substring(0, documentName.LastIndexOf('/'));
                fileName = documentName.Substring(documentName.LastIndexOf('/') + 1);
            }

            return new Document
            {
                Title = documentName,
                EditLink = editLink,
                RootUrl = rootUrl,
                RawRootUrl = rawRootUrl,
                Format = project.Format,
                LocalDirectory = localDirectory,
                FileName = fileName,
                Contributors = await GetContributors(commitHistoryUrl, token),
                Version = version,
                Content = await DownloadWebContentAsStringAsync(rawDocumentUrl, token)
            };
        }

        public async Task<List<VersionInfo>> GetVersions(Project project)
        {
            try
            {
                return (await GetReleasesAsync(project))
                    .OrderByDescending(r => r.PublishedAt)
                    .Select(r => new VersionInfo
                    {
                        Name = r.TagName,
                        DisplayName = r.TagName
                    }).ToList();
            }
            catch (Exception ex)
            {
                //TODO: It may not be a good idea to hide the error!
                Logger.LogError(ex.Message, ex);
                return new List<VersionInfo>();
            }
        }

        public async Task<DocumentResource> GetResource(Project project, string resourceName, string version)
        {
            var rawRootUrl = CalculateRawRootUrl(project.GetGitHubUrl(version));
            var content = await DownloadWebContentAsByteArrayAsync(
                rawRootUrl + resourceName,
                project.GetGitHubAccessTokenOrNull()
            );

            return new DocumentResource(content);
        }

        private async Task<IReadOnlyList<Release>> GetReleasesAsync(Project project)
        {
            var url = project.GetGitHubUrl();
            var ownerName = GetOwnerNameFromUrl(url);
            var repositoryName = GetRepositoryNameFromUrl(url);
            var gitHubClient = CreateGitHubClient(project.GetGitHubAccessTokenOrNull());

            return await gitHubClient
                .Repository
                .Release
                .GetAll(ownerName, repositoryName);
        }

        private static GitHubClient CreateGitHubClient(string token = null)
        {
            //TODO: Why hard-coded "abpframework"? Should be configurable?
            return token.IsNullOrWhiteSpace()
                ? new GitHubClient(new ProductHeaderValue("abpframework"))
                : new GitHubClient(new ProductHeaderValue("abpframework"), new InMemoryCredentialStore(new Credentials(token)));
        }

        protected virtual string GetOwnerNameFromUrl(string url)
        {
            try
            {
                var urlStartingAfterFirstSlash = url.Substring(url.IndexOf("github.com/",StringComparison.OrdinalIgnoreCase) + "github.com/".Length);
                return urlStartingAfterFirstSlash.Substring(0, urlStartingAfterFirstSlash.IndexOf('/'));
            }
            catch (Exception)
            {
                throw new Exception($"Github url is not valid: {url}");
            }
        }

        protected virtual string GetRepositoryNameFromUrl(string url)
        {
            try
            {
                var urlStartingAfterFirstSlash = url.Substring(url.IndexOf("github.com/", StringComparison.OrdinalIgnoreCase) + "github.com/".Length);
                var urlStartingAfterSecondSlash = urlStartingAfterFirstSlash.Substring(urlStartingAfterFirstSlash.IndexOf('/') + 1);
                return urlStartingAfterSecondSlash.Substring(0, urlStartingAfterSecondSlash.IndexOf('/'));
            }
            catch (Exception)
            {
                throw new Exception($"Github url is not valid: {url}");
            }
        }

        private async Task<string> DownloadWebContentAsStringAsync(string rawUrl, string token)
        {
            try
            {
                using (var webClient = new WebClient())
                {
                    if (!token.IsNullOrWhiteSpace())
                    {
                        webClient.Headers.Add("Authorization", "token " + token);
                    }
                    webClient.Headers.Add("User-Agent", "request");

                    return await webClient.DownloadStringTaskAsync(new Uri(rawUrl));
                }
            }
            catch (Exception ex)
            {
                //TODO: Only handle when document is really not available
                Logger.LogWarning(ex.Message, ex);
                throw new DocumentNotFoundException(rawUrl);
            }
        }

        private async Task<byte[]> DownloadWebContentAsByteArrayAsync(string rawUrl, string token)
        {
            try
            {
                using (var webClient = new WebClient())
                {
                    if (!token.IsNullOrWhiteSpace())
                    {
                        webClient.Headers.Add("Authorization", "token " + token);
                    }

                    return await webClient.DownloadDataTaskAsync(new Uri(rawUrl));
                }
            }
            catch (Exception ex)
            {
                //TODO: Only handle when resource is really not available
                Logger.LogWarning(ex.Message, ex);
                throw new ResourceNotFoundException(rawUrl);
            }
        }

        private async Task<List<DocumentContributor>> GetContributors(string url, string token)
        {
            var contributors = new List<DocumentContributor>();

            try
            {
                var commitsJsonAsString = await DownloadWebContentAsStringAsync(url, token);

                var commits = JArray.Parse(commitsJsonAsString);

                foreach (var commit in commits)
                {
                    var author = commit["author"];

                    if (contributors.All(c => c.Username != (string) author["login"]))
                    {
                        contributors.Add(new DocumentContributor
                        {
                            Username = (string)author["login"],
                            UserProfileUrl = (string)author["html_url"],
                            AvatarUrl = (string)author["avatar_url"]
                        });
                    }
                }

                contributors.Reverse();
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex.Message);
            }


            return contributors;
        }

        private static string CalculateRawRootUrl(string rootUrl)
        {
            return rootUrl
                .Replace("github.com", "raw.githubusercontent.com")
                .ReplaceFirst("/tree/", "/");
        }
    }
}