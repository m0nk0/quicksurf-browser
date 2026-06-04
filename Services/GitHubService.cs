using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace QuickSurfBrowser.Services
{
    public class GitHubRepo
    {
        public string Name { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Description { get; set; } = "";
        public int StargazersCount { get; set; }
        public int ForksCount { get; set; }
        public string HtmlUrl { get; set; } = "";
        public string Language { get; set; } = "";
    }

    public class GitHubService
    {
        private readonly HttpClient _httpClient = new HttpClient();

        public GitHubService()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "QuickSurfBrowser");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        }

        public async Task<List<GitHubRepo>> GetTrendingAIReposAsync(int count = 8)
        {
            try
            {
                // Поиск AI репозиториев с сортировкой по звёздам
                string url = "https://api.github.com/search/repositories?q=topic:artificial-intelligence+topic:llm+topic:machine-learning&sort=stars&order=desc&per_page=" + count;
                
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return GetFallbackRepos();

                var json = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<GitHubSearchResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                if (data?.Items == null || data.Items.Count == 0)
                    return GetFallbackRepos();

                var repos = new List<GitHubRepo>();
                foreach (var item in data.Items)
                {
                    repos.Add(new GitHubRepo
                    {
                        Name = item.Name,
                        FullName = item.FullName,
                        Description = item.Description,
                        StargazersCount = item.StargazersCount,
                        ForksCount = item.ForksCount,
                        HtmlUrl = item.HtmlUrl,
                        Language = item.Language
                    });
                }
                return repos;
            }
            catch (Exception)
            {
                return GetFallbackRepos();
            }
        }

        private List<GitHubRepo> GetFallbackRepos()
        {
            // Запасные репозитории (если API не отвечает)
            return new List<GitHubRepo>
            {
                new GitHubRepo { Name = "transformers", FullName = "huggingface/transformers", StargazersCount = 98000, ForksCount = 20000, HtmlUrl = "https://github.com/huggingface/transformers", Language = "Python" },
                new GitHubRepo { Name = "pytorch", FullName = "pytorch/pytorch", StargazersCount = 68000, ForksCount = 18000, HtmlUrl = "https://github.com/pytorch/pytorch", Language = "C++" },
                new GitHubRepo { Name = "tensorflow", FullName = "tensorflow/tensorflow", StargazersCount = 175000, ForksCount = 88000, HtmlUrl = "https://github.com/tensorflow/tensorflow", Language = "C++" },
                new GitHubRepo { Name = "langchain", FullName = "langchain-ai/langchain", StargazersCount = 65000, ForksCount = 9000, HtmlUrl = "https://github.com/langchain-ai/langchain", Language = "Python" },
                new GitHubRepo { Name = "llama.cpp", FullName = "ggerganov/llama.cpp", StargazersCount = 48000, ForksCount = 6800, HtmlUrl = "https://github.com/ggerganov/llama.cpp", Language = "C++" },
                new GitHubRepo { Name = "gpt4all", FullName = "nomic-ai/gpt4all", StargazersCount = 56000, ForksCount = 6200, HtmlUrl = "https://github.com/nomic-ai/gpt4all", Language = "C++" },
                new GitHubRepo { Name = "ollama", FullName = "ollama/ollama", StargazersCount = 42000, ForksCount = 3400, HtmlUrl = "https://github.com/ollama/ollama", Language = "Go" },
                new GitHubRepo { Name = "comfyui", FullName = "comfyanonymous/ComfyUI", StargazersCount = 28000, ForksCount = 2800, HtmlUrl = "https://github.com/comfyanonymous/ComfyUI", Language = "Python" }
            };
        }
    }

    public class GitHubSearchResponse
    {
        public List<GitHubRepoItem> Items { get; set; } = new();
    }

    public class GitHubRepoItem
    {
        public string Name { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Description { get; set; } = "";
        public int StargazersCount { get; set; }
        public int ForksCount { get; set; }
        public string HtmlUrl { get; set; } = "";
        public string Language { get; set; } = "";
    }
}