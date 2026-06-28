using System.Net.Http.Json;
using System.Text.Json;
using RunpodOrchestrator.Application.Common.RunPod;
using RunpodOrchestrator.Application.Exceptions;
using RunpodOrchestrator.Application.Ports.RunPod;
using RunpodOrchestrator.Domain.Exceptions;
using RunpodOrchestrator.Infrastructure.Services.RunPod.GraphQL;
using Microsoft.Extensions.Logging;

namespace RunpodOrchestrator.Infrastructure.Services.RunPod;

public sealed class RunPodApiClient : IRunPodApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<RunPodApiClient> _logger;

    public RunPodApiClient(HttpClient httpClient, ILogger<RunPodApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<PodStatusResponse> GetPodStatusAsync(string podId, CancellationToken ct = default)
    {
        ValidatePodId(podId);

        var serializedPodId = JsonSerializer.Serialize(podId);
        var query = $@"query {{
              pod(input: {{podId: {serializedPodId}}}) {{
                id
                name
                runtime {{
                  uptimeInSeconds
                  ports {{
                    ip
                    isIpPublic
                    privatePort
                    publicPort
                  }}
                }}
              }}
            }}";

        var data = await SendGraphQLAsync<PodStatusData>(query, ct).ConfigureAwait(false);

        if (data.Pod is null)
        {
            throw new DomainNotFoundException(ApplicationErrorMessages.RunPod.POD_NOT_FOUND);
        }

        return MapPodStatus(data.Pod);
    }

    public async Task<PodResumeResponse> ResumePodAsync(string podId, int gpuCount, CancellationToken ct = default)
    {
        ValidatePodId(podId);

        if (gpuCount <= 0)
        {
            throw new DomainValidationException("GPU count must be greater than zero.");
        }

        _logger.LogInformation("Resuming pod {PodId} with {GpuCount} GPU(s).", podId, gpuCount);

        var serializedPodId = JsonSerializer.Serialize(podId);
        var mutation = $@"mutation {{
              podResume(input: {{
                podId: {serializedPodId},
                gpuCount: {gpuCount}
              }}) {{
                id
                desiredStatus
              }}
            }}";

        var data = await SendGraphQLAsync<PodResumeData>(mutation, ct).ConfigureAwait(false);

        if (data.PodResume is null)
        {
            throw new RunPodApiException(ApplicationErrorMessages.RunPod.EMPTY_RESPONSE);
        }

        _logger.LogInformation("Pod {PodId} resume completed. Desired status: {DesiredStatus}.", podId, data.PodResume.DesiredStatus);
        var fields = MapPodMutationFields(data.PodResume);
        return new PodResumeResponse { Id = fields.Id, DesiredStatus = fields.DesiredStatus };
    }

    public async Task<PodStopResponse> StopPodAsync(string podId, CancellationToken ct = default)
    {
        ValidatePodId(podId);

        _logger.LogInformation("Stopping pod {PodId}.", podId);

        var serializedPodId = JsonSerializer.Serialize(podId);
        var mutation = $@"mutation {{
              podStop(input: {{podId: {serializedPodId}}}) {{
                id
                desiredStatus
              }}
            }}";

        var data = await SendGraphQLAsync<PodStopData>(mutation, ct).ConfigureAwait(false);

        if (data.PodStop is null)
        {
            throw new RunPodApiException(ApplicationErrorMessages.RunPod.EMPTY_RESPONSE);
        }

        _logger.LogInformation("Pod {PodId} stop completed. Desired status: {DesiredStatus}.", podId, data.PodStop.DesiredStatus);
        var fields = MapPodMutationFields(data.PodStop);
        return new PodStopResponse { Id = fields.Id, DesiredStatus = fields.DesiredStatus };
    }

    public async Task<PodTerminateResponse> TerminatePodAsync(string podId, CancellationToken ct = default)
    {
        ValidatePodId(podId);

        _logger.LogInformation("Terminating pod {PodId}.", podId);

        var serializedPodId = JsonSerializer.Serialize(podId);
        var mutation = $@"mutation {{
              podTerminate(input: {{podId: {serializedPodId}}})
            }}";

        await SendGraphQLVoidMutationAsync(mutation, ct).ConfigureAwait(false);

        _logger.LogInformation("Pod {PodId} terminate completed.", podId);
        return new PodTerminateResponse { Id = podId };
    }

    public async Task<GpuAvailabilityResponse> GetGpuAvailabilityAsync(string gpuTypeId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(gpuTypeId))
        {
            throw new DomainValidationException("GPU type ID is required.");
        }

        var serializedGpuTypeId = JsonSerializer.Serialize(gpuTypeId);
        var query = $@"query {{
              gpuTypes(input: {{ id: {serializedGpuTypeId} }}) {{
                id
                displayName
                lowestPrice(input: {{ gpuCount: 1, secureCloud: true }}) {{
                  stockStatus
                  availableGpuCounts
                }}
              }}
            }}";

        var data = await SendGraphQLAsync<GpuTypesData>(query, ct).ConfigureAwait(false);

        if (data.GpuTypes is null || data.GpuTypes.Count == 0)
        {
            throw new DomainNotFoundException(ApplicationErrorMessages.RunPod.GPU_TYPE_NOT_FOUND);
        }

        return new GpuAvailabilityResponse
        {
            GpuTypes = data.GpuTypes.Select(MapGpuType).ToList()
        };
    }

    public async Task<PodListResponse> ListPodsAsync(CancellationToken ct = default)
    {
        const string query = @"query {
              myself {
                pods {
                  id
                  name
                  desiredStatus
                  runtime {
                    uptimeInSeconds
                  }
                  machine {
                    gpuDisplayName
                  }
                }
              }
            }";

        var data = await SendGraphQLAsync<MyselfPodsData>(query, ct).ConfigureAwait(false);
        var pods = data.Myself?.Pods ?? [];

        return new PodListResponse
        {
            Pods = pods
                .Where(p => !string.IsNullOrWhiteSpace(p.Id))
                .Select(MapPodSummary)
                .ToList()
        };
    }

    public async Task<PodDeployResponse> DeployPodAsync(PodDeployRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.GpuTypeId))
        {
            throw new DomainValidationException("GPU type ID is required for pod deployment.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new DomainValidationException("Pod name is required for pod deployment.");
        }

        var hasTemplate = !string.IsNullOrWhiteSpace(request.TemplateId);

        if (hasTemplate)
        {
            ValidateTemplateDeployRequest(request);
        }
        else
        {
            ValidateImageDeployRequest(request);
        }

        if (hasTemplate)
        {
            _logger.LogInformation(
                "Deploying new pod {PodName} with template {TemplateId} on GPU {GpuTypeId}.",
                request.Name,
                request.TemplateId,
                request.GpuTypeId);
        }
        else
        {
            _logger.LogInformation(
                "Deploying new pod {PodName} with GPU {GpuTypeId}.",
                request.Name,
                request.GpuTypeId);
        }

        var serializedGpuTypeId = JsonSerializer.Serialize(request.GpuTypeId);
        var serializedName = JsonSerializer.Serialize(request.Name);
        var networkVolumeField = string.IsNullOrWhiteSpace(request.NetworkVolumeId)
            ? string.Empty
            : $", networkVolumeId: {JsonSerializer.Serialize(request.NetworkVolumeId)}";
        var envField = SerializePodEnvironmentField(request.Environment);

        string mutation;
        if (hasTemplate)
        {
            var serializedTemplateId = JsonSerializer.Serialize(request.TemplateId);
            mutation = $@"mutation {{
              podFindAndDeployOnDemand(input: {{
                cloudType: ALL,
                gpuCount: {request.GpuCount},
                gpuTypeId: {serializedGpuTypeId},
                name: {serializedName},
                templateId: {serializedTemplateId}{networkVolumeField}{envField}
              }}) {{
                id
                desiredStatus
                machine {{
                  gpuDisplayName
                }}
              }}
            }}";
        }
        else
        {
            var serializedImageName = JsonSerializer.Serialize(request.ImageName);
            var serializedPorts = JsonSerializer.Serialize(request.Ports);
            var serializedVolumeMountPath = JsonSerializer.Serialize(request.VolumeMountPath);
            mutation = $@"mutation {{
              podFindAndDeployOnDemand(input: {{
                cloudType: ALL,
                gpuCount: {request.GpuCount},
                volumeInGb: {request.VolumeInGb},
                containerDiskInGb: {request.ContainerDiskInGb},
                gpuTypeId: {serializedGpuTypeId},
                name: {serializedName},
                imageName: {serializedImageName},
                ports: {serializedPorts},
                volumeMountPath: {serializedVolumeMountPath}{networkVolumeField}{envField}
              }}) {{
                id
                desiredStatus
                machine {{
                  gpuDisplayName
                }}
              }}
            }}";
        }

        var data = await SendGraphQLAsync<PodDeployData>(mutation, ct).ConfigureAwait(false);

        if (data.PodFindAndDeployOnDemand is null || string.IsNullOrWhiteSpace(data.PodFindAndDeployOnDemand.Id))
        {
            throw new RunPodApiException(ApplicationErrorMessages.RunPod.POD_DEPLOY_FAILED);
        }

        _logger.LogInformation(
            "Pod {PodId} deployed. Desired status: {DesiredStatus}.",
            data.PodFindAndDeployOnDemand.Id,
            data.PodFindAndDeployOnDemand.DesiredStatus);

        return new PodDeployResponse
        {
            Id = data.PodFindAndDeployOnDemand.Id,
            DesiredStatus = data.PodFindAndDeployOnDemand.DesiredStatus,
            GpuTypeId = data.PodFindAndDeployOnDemand.Machine?.GpuDisplayName ?? request.GpuTypeId
        };
    }

    private static void ValidateTemplateDeployRequest(PodDeployRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TemplateId))
        {
            throw new DomainValidationException("Pod template ID is required for template-based deployment.");
        }
    }

    private static void ValidateImageDeployRequest(PodDeployRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ImageName))
        {
            throw new DomainValidationException("Pod image name is required for pod deployment.");
        }

        if (string.IsNullOrWhiteSpace(request.Ports))
        {
            throw new DomainValidationException("Pod ports are required for pod deployment.");
        }

        if (string.IsNullOrWhiteSpace(request.VolumeMountPath))
        {
            throw new DomainValidationException("Volume mount path is required for pod deployment.");
        }

        if (request.VolumeInGb is null or <= 0)
        {
            throw new DomainValidationException("Volume size must be greater than zero for pod deployment.");
        }

        if (request.ContainerDiskInGb is null or <= 0)
        {
            throw new DomainValidationException("Container disk size must be greater than zero for pod deployment.");
        }
    }

    private async Task SendGraphQLVoidMutationAsync(string query, CancellationToken ct) =>
        await SendGraphQLRequestAsync<JsonElement>(query, ct).ConfigureAwait(false);

    private async Task<TData> SendGraphQLAsync<TData>(string query, CancellationToken ct)
    {
        var graphQlResponse = await SendGraphQLRequestAsync<TData>(query, ct).ConfigureAwait(false);

        if (graphQlResponse.Data is null)
        {
            throw new RunPodApiException(ApplicationErrorMessages.RunPod.EMPTY_RESPONSE);
        }

        return graphQlResponse.Data;
    }

    private async Task<GraphQLResponse<TData>> SendGraphQLRequestAsync<TData>(string query, CancellationToken ct)
    {
        var request = new GraphQLRequest { Query = query };

        try
        {
            var endpoint = _httpClient.BaseAddress
                ?? throw new InvalidOperationException("RunPod HttpClient BaseAddress is not configured.");

            using var response = await _httpClient.PostAsJsonAsync(endpoint, request, JsonOptions, ct)
                .ConfigureAwait(false);

            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "RunPod GraphQL request failed with status {StatusCode}. Body: {Body}",
                    (int)response.StatusCode,
                    body);

                throw new RunPodApiException(
                    $"{ApplicationErrorMessages.RunPod.HTTP_REQUEST_FAILED} HTTP {(int)response.StatusCode}.");
            }

            var graphQlResponse = JsonSerializer.Deserialize<GraphQLResponse<TData>>(body, JsonOptions);

            if (graphQlResponse is null)
            {
                throw new RunPodApiException(ApplicationErrorMessages.RunPod.EMPTY_RESPONSE);
            }

            if (graphQlResponse.Errors is { Count: > 0 })
            {
                var messages = string.Join("; ", graphQlResponse.Errors.Select(e => e.Message ?? "Unknown error"));
                _logger.LogError("RunPod GraphQL returned errors: {Errors}", messages);
                throw new RunPodApiException($"{ApplicationErrorMessages.RunPod.GRAPHQL_ERRORS} {messages}");
            }

            return graphQlResponse;
        }
        catch (RunPodApiException)
        {
            throw;
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "RunPod GraphQL request timed out.");
            throw new RunPodApiException(ApplicationErrorMessages.RunPod.HTTP_REQUEST_FAILED, ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "RunPod GraphQL HTTP request failed.");
            throw new RunPodApiException(ApplicationErrorMessages.RunPod.HTTP_REQUEST_FAILED, ex);
        }
    }

    private static void ValidatePodId(string podId)
    {
        if (string.IsNullOrWhiteSpace(podId))
        {
            throw new DomainValidationException(DomainErrorMessages.Pod.POD_ID_REQUIRED);
        }
    }

    private static PodStatusResponse MapPodStatus(PodGraphQL pod) =>
        new()
        {
            Id = pod.Id ?? string.Empty,
            Name = pod.Name ?? string.Empty,
            Runtime = pod.Runtime is null ? null : MapRuntime(pod.Runtime)
        };

    private static (string Id, string DesiredStatus) MapPodMutationFields(PodMutationGraphQL pod) =>
        (pod.Id ?? string.Empty, pod.DesiredStatus ?? string.Empty);

    private static PodRuntimeDto MapRuntime(PodRuntimeGraphQL runtime) =>
        new()
        {
            UptimeInSeconds = runtime.UptimeInSeconds,
            Ports = runtime.Ports?.Select(MapPort).ToList()
        };

    private static PodPortDto MapPort(PodPortGraphQL port) =>
        new()
        {
            Ip = port.Ip,
            IsPublic = port.IsPublic,
            PrivatePort = port.PrivatePort,
            PublicPort = port.PublicPort
        };

    private static GpuTypeDto MapGpuType(GpuTypeGraphQL gpuType) =>
        new()
        {
            Id = gpuType.Id ?? string.Empty,
            DisplayName = gpuType.DisplayName ?? string.Empty,
            LowestPrice = gpuType.LowestPrice is null ? null : new GpuLowestPriceDto
            {
                StockStatus = gpuType.LowestPrice.StockStatus,
                AvailableGpuCounts = gpuType.LowestPrice.AvailableGpuCounts
            }
        };

    private static PodSummaryResponse MapPodSummary(PodListItemGraphQL pod) =>
        new()
        {
            Id = pod.Id ?? string.Empty,
            Name = pod.Name ?? string.Empty,
            DesiredStatus = pod.DesiredStatus,
            GpuTypeId = pod.Machine?.GpuDisplayName,
            IsRunning = pod.Runtime is not null
        };

    private static string SerializePodEnvironmentField(IReadOnlyDictionary<string, string> environment)
    {
        if (environment.Count == 0)
        {
            return string.Empty;
        }

        var entries = environment
            .Select(kvp =>
                $"{{ key: {JsonSerializer.Serialize(kvp.Key)}, value: {JsonSerializer.Serialize(kvp.Value)} }}");

        return $", env: [{string.Join(", ", entries)}]";
    }
}
