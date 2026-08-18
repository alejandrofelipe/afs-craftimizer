using Artificer.Application.CraftingLists;
using Artificer.Data;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Artificer.Test.CraftingLists;

[TestClass]
public class MarketboardHelperTests
{
    private const uint ItemId = 5333;
    private const uint WorldId = 42;

    [TestMethod]
    public void ParseScope_ListingsOutOfOrder_UsesLowestPriceWorldAndAllQuantities()
    {
        var cachedAt = new DateTime(2026, 8, 18, 12, 30, 0, DateTimeKind.Utc);
        const string json = """
            {
              "minPrice": 80,
              "listings": [
                { "pricePerUnit": 120, "quantity": 4, "worldName": "WorldA" },
                { "pricePerUnit": 80, "quantity": 7, "worldName": "WorldB" },
                { "pricePerUnit": 100, "quantity": 3, "worldName": "WorldC" }
              ]
            }
            """;

        var result = MarketboardHelper.ParseScope(5333, "Aether", json, cachedAt);

        Assert.AreEqual(
            new MarketScopePrice(5333, "Aether", 80, "WorldB", 14, cachedAt),
            result);
    }

    [TestMethod]
    public void ParseScope_WithoutListings_UsesMinPriceAndEmptyAvailability()
    {
        var cachedAt = new DateTime(2026, 8, 18, 12, 30, 0, DateTimeKind.Utc);
        const string json = """
            {
              "minPrice": 95,
              "listings": []
            }
            """;

        var result = MarketboardHelper.ParseScope(5333, "WorldA", json, cachedAt);

        Assert.AreEqual(
            new MarketScopePrice(5333, "WorldA", 95, string.Empty, 0, cachedAt),
            result);
    }

    [TestMethod]
    public void ParseScope_WithInvalidJson_ReturnsNull()
    {
        var result = MarketboardHelper.ParseScope(
            5333,
            "WorldA",
            "{ invalid json",
            new DateTime(2026, 8, 18, 12, 30, 0, DateTimeKind.Utc));

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ParseScope_WithJsonNull_ReturnsNull()
    {
        var result = MarketboardHelper.ParseScope(
            5333,
            "WorldA",
            "null",
            new DateTime(2026, 8, 18, 12, 30, 0, DateTimeKind.Utc));

        Assert.IsNull(result);
    }

    [TestMethod]
    public void Combine_WithWorldAndDataCenter_UsesEachScopeAndOldestTimestamp()
    {
        var worldCachedAt = new DateTime(2026, 8, 18, 12, 35, 0, DateTimeKind.Utc);
        var dataCenterCachedAt = new DateTime(2026, 8, 18, 12, 30, 0, DateTimeKind.Utc);
        var currentWorld = new MarketScopePrice(
            5333, "WorldA", 120, "WorldA", 14, worldCachedAt);
        var dataCenter = new MarketScopePrice(
            5333, "Aether", 80, "WorldB", 27, dataCenterCachedAt);

        var combined = MarketboardHelper.Combine(currentWorld, dataCenter);

        Assert.AreEqual(
            new MarketPrice(5333, 120, 80, "WorldB", 14, dataCenterCachedAt),
            combined);
    }

    [TestMethod]
    public void Combine_WithOlderWorldSnapshot_UsesWorldTimestamp()
    {
        var worldCachedAt = new DateTime(2026, 8, 18, 12, 25, 0, DateTimeKind.Utc);
        var dataCenterCachedAt = new DateTime(2026, 8, 18, 12, 30, 0, DateTimeKind.Utc);
        var currentWorld = new MarketScopePrice(
            5333, "WorldA", 120, "WorldA", 14, worldCachedAt);
        var dataCenter = new MarketScopePrice(
            5333, "Aether", 80, "WorldB", 27, dataCenterCachedAt);

        var combined = MarketboardHelper.Combine(currentWorld, dataCenter);

        Assert.AreEqual(
            new MarketPrice(5333, 120, 80, "WorldB", 14, worldCachedAt),
            combined);
    }

    [TestMethod]
    public void Combine_WithoutDataCenter_ReusesCurrentWorld()
    {
        var cachedAt = new DateTime(2026, 8, 18, 12, 30, 0, DateTimeKind.Utc);
        var currentWorld = new MarketScopePrice(
            5333, "WorldA", 120, "WorldA", 14, cachedAt);

        var combined = MarketboardHelper.Combine(currentWorld, null);

        Assert.AreEqual(
            new MarketPrice(5333, 120, 120, "WorldA", 14, cachedAt),
            combined);
    }

    [TestMethod]
    public void Combine_WithoutCurrentWorld_ReturnsNull()
    {
        var dataCenter = new MarketScopePrice(
            5333,
            "Aether",
            80,
            "WorldB",
            27,
            new DateTime(2026, 8, 18, 12, 30, 0, DateTimeKind.Utc));

        var combined = MarketboardHelper.Combine(null, dataCenter);

        Assert.IsNull(combined);
    }

    [TestMethod]
    public async Task GetPriceAsync_WithFreshWorldAndDataCenterCache_DoesNotCallTransport()
    {
        using var fixture = new RepositoryFixture();
        var cachedAt = RecentUtc();
        var cachedWorld = new MarketScopePrice(ItemId, "42", 120, "WorldA", 14, cachedAt);
        var cachedDataCenter = new MarketScopePrice(
            ItemId, "Aether", 80, "WorldB", 27, cachedAt);
        fixture.Repository.SaveScopePrice(cachedWorld);
        fixture.Repository.SaveScopePrice(cachedDataCenter);
        var transport = new FakeMarketboardTransport();
        var dispatcher = new RecordingFrameworkDispatcher();
        using var helper = CreateHelper(fixture.Repository, transport, dispatcher);
        using var tokenSource = new CancellationTokenSource();

        var result = await helper.GetPriceAsync(
            ItemId, WorldId, "Aether", ttlMinutes: 10, tokenSource.Token);

        Assert.AreEqual(
            new MarketPrice(ItemId, 120, 80, "WorldB", 14, cachedAt),
            result);
        Assert.AreEqual(0, transport.Calls.Count);
        Assert.AreEqual(
            "CacheRead,CacheRead",
            string.Join(',', dispatcher.Calls.Select(call => call.Operation)));
        Assert.IsTrue(dispatcher.Calls.TrueForAll(call => call.ExecutedInsideDispatcher));
        Assert.IsTrue(dispatcher.Calls.TrueForAll(call => call.ActionCompleted));
        Assert.IsTrue(dispatcher.Calls.TrueForAll(call => call.Token == tokenSource.Token));
        Assert.AreEqual(cachedWorld, dispatcher.Calls[0].CallbackValue);
        Assert.AreEqual(cachedDataCenter, dispatcher.Calls[1].CallbackValue);
    }

    [TestMethod]
    public async Task GetPriceAsync_WithStaleCacheAndSuccessfulNetwork_ReplacesEachScopeSnapshot()
    {
        using var fixture = new RepositoryFixture();
        var staleAt = StaleUtc();
        var worldFetchedAt = new DateTime(2026, 8, 18, 15, 0, 0, DateTimeKind.Utc);
        var dataCenterFetchedAt = new DateTime(2026, 8, 18, 15, 0, 1, DateTimeKind.Utc);
        var staleWorld = new MarketScopePrice(ItemId, "42", 999, "OldWorld", 1, staleAt);
        var staleDataCenter = new MarketScopePrice(
            ItemId, "Aether", 888, "OldDc", 2, staleAt);
        var fetchedWorld = new MarketScopePrice(
            ItemId, "42", 120, "WorldA", 14, worldFetchedAt);
        var fetchedDataCenter = new MarketScopePrice(
            ItemId, "Aether", 80, "WorldB", 27, dataCenterFetchedAt);
        fixture.Repository.SaveScopePrice(staleWorld);
        fixture.Repository.SaveScopePrice(staleDataCenter);
        var transport = new FakeMarketboardTransport
        {
            OnFetchWorld = (_, _, _) => Task.FromResult<MarketScopePrice?>(fetchedWorld),
            OnFetchScope = (_, _, _) => Task.FromResult<MarketScopePrice?>(fetchedDataCenter)
        };
        var dispatcher = new RecordingFrameworkDispatcher();
        using var helper = CreateHelper(fixture.Repository, transport, dispatcher);
        using var tokenSource = new CancellationTokenSource();

        var result = await helper.GetPriceAsync(
            ItemId, WorldId, "Aether", ttlMinutes: 10, tokenSource.Token);

        Assert.AreEqual(
            new MarketPrice(ItemId, 120, 80, "WorldB", 14, worldFetchedAt),
            result);
        Assert.AreEqual(
            fetchedWorld,
            fixture.Repository.GetCachedScopePrice(ItemId, "42"));
        Assert.AreEqual(
            fetchedDataCenter,
            fixture.Repository.GetCachedScopePrice(ItemId, "Aether"));
        Assert.AreEqual(
            "world:42,scope:Aether",
            string.Join(',', transport.Calls.Select(call => $"{call.Kind}:{call.Scope}")));
        Assert.IsTrue(transport.Calls.TrueForAll(call => call.Token == tokenSource.Token));
        Assert.AreEqual(
            "CacheRead,CacheWrite,CacheRead,CacheWrite",
            string.Join(',', dispatcher.Calls.Select(call => call.Operation)));
        Assert.IsTrue(dispatcher.Calls.TrueForAll(call => call.ExecutedInsideDispatcher));
        Assert.IsTrue(dispatcher.Calls.TrueForAll(call => call.ActionCompleted));
        Assert.AreEqual(staleWorld, dispatcher.Calls[0].CallbackValue);
        Assert.AreEqual(fetchedWorld, dispatcher.Calls[1].CallbackValue);
        Assert.AreEqual(staleDataCenter, dispatcher.Calls[2].CallbackValue);
        Assert.AreEqual(fetchedDataCenter, dispatcher.Calls[3].CallbackValue);
    }

    [TestMethod]
    public async Task GetPriceAsync_WhenBothNetworkRequestsFail_KeepsEachScopesOwnStaleSnapshot()
    {
        using var fixture = new RepositoryFixture();
        var staleAt = StaleUtc();
        var staleWorld = new MarketScopePrice(ItemId, "42", 120, "WorldA", 14, staleAt);
        var staleDataCenter = new MarketScopePrice(ItemId, "Aether", 80, "WorldB", 27, staleAt);
        fixture.Repository.SaveScopePrice(staleWorld);
        fixture.Repository.SaveScopePrice(staleDataCenter);
        var transport = new FakeMarketboardTransport
        {
            OnFetchWorld = (_, _, _) =>
                Task.FromException<MarketScopePrice?>(new HttpRequestException("world offline")),
            OnFetchScope = (_, _, _) =>
                Task.FromException<MarketScopePrice?>(new HttpRequestException("dc offline"))
        };
        var dispatcher = new RecordingFrameworkDispatcher();
        using var helper = CreateHelper(fixture.Repository, transport, dispatcher);

        var result = await helper.GetPriceAsync(ItemId, WorldId, "Aether", ttlMinutes: 10);

        Assert.AreEqual(
            new MarketPrice(ItemId, 120, 80, "WorldB", 14, staleAt),
            result);
        Assert.AreEqual(staleWorld, fixture.Repository.GetCachedScopePrice(ItemId, "42"));
        Assert.AreEqual(
            staleDataCenter,
            fixture.Repository.GetCachedScopePrice(ItemId, "Aether"));
        Assert.AreEqual(
            "world:42,scope:Aether",
            string.Join(',', transport.Calls.Select(call => $"{call.Kind}:{call.Scope}")));
        Assert.AreEqual(
            "CacheRead,CacheRead",
            string.Join(',', dispatcher.Calls.Select(call => call.Operation)));
    }

    [TestMethod]
    public async Task GetPriceAsync_WhenDataCenterFailsWithoutItsCache_ReusesWorldAndDoesNotReadAnotherDcKey()
    {
        using var fixture = new RepositoryFixture();
        var fetchedAt = new DateTime(2026, 8, 18, 15, 0, 0, DateTimeKind.Utc);
        var foreignDataCenter = new MarketScopePrice(
            ItemId, "Primal", 1, "ForeignWorld", 999, RecentUtc());
        fixture.Repository.SaveScopePrice(foreignDataCenter);
        var transport = new FakeMarketboardTransport
        {
            OnFetchWorld = (_, _, _) => Task.FromResult<MarketScopePrice?>(
                new(ItemId, "42", 120, "WorldA", 14, fetchedAt)),
            OnFetchScope = (_, _, _) => Task.FromResult<MarketScopePrice?>(null)
        };
        var dispatcher = new RecordingFrameworkDispatcher();
        using var helper = CreateHelper(fixture.Repository, transport, dispatcher);

        var result = await helper.GetPriceAsync(ItemId, WorldId, "Aether", ttlMinutes: 10);

        Assert.AreEqual(
            new MarketPrice(ItemId, 120, 120, "WorldA", 14, fetchedAt),
            result);
        Assert.IsNull(fixture.Repository.GetCachedScopePrice(ItemId, "Aether"));
        Assert.AreEqual(
            foreignDataCenter,
            fixture.Repository.GetCachedScopePrice(ItemId, "Primal"));
        Assert.AreEqual(
            "world:42,scope:Aether",
            string.Join(',', transport.Calls.Select(call => $"{call.Kind}:{call.Scope}")));
    }

    [TestMethod]
    public async Task GetPriceAsync_WhenCancelledAfterWorld_ThrowsBeforeDataCenterRequestOrCacheWrite()
    {
        using var fixture = new RepositoryFixture();
        using var tokenSource = new CancellationTokenSource();
        var transport = new FakeMarketboardTransport
        {
            OnFetchWorld = (_, _, _) =>
            {
                tokenSource.Cancel();
                return Task.FromResult<MarketScopePrice?>(new(
                    ItemId,
                    "42",
                    120,
                    "WorldA",
                    14,
                    new DateTime(2026, 8, 18, 15, 0, 0, DateTimeKind.Utc)));
            }
        };
        var dispatcher = new RecordingFrameworkDispatcher();
        using var helper = CreateHelper(fixture.Repository, transport, dispatcher);

        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() =>
            helper.GetPriceAsync(ItemId, WorldId, "Aether", 10, tokenSource.Token));

        Assert.AreEqual(
            "world:42",
            string.Join(',', transport.Calls.Select(call => $"{call.Kind}:{call.Scope}")));
        Assert.AreEqual(tokenSource.Token, transport.Calls.Single().Token);
        Assert.IsNull(fixture.Repository.GetCachedScopePrice(ItemId, "42"));
        Assert.AreEqual(
            "CacheRead",
            string.Join(',', dispatcher.Calls.Select(call => call.Operation)));
    }

    [TestMethod]
    public async Task GetPriceAsync_WhenTransportThrowsOperationCanceledException_DoesNotReturnStaleCache()
    {
        using var fixture = new RepositoryFixture();
        var staleAt = StaleUtc();
        fixture.Repository.SaveScopePrice(
            new MarketScopePrice(ItemId, "42", 120, "WorldA", 14, staleAt));
        fixture.Repository.SaveScopePrice(
            new MarketScopePrice(ItemId, "Aether", 80, "WorldB", 27, staleAt));
        var cancellation = new OperationCanceledException("transport cancelled");
        var transport = new FakeMarketboardTransport
        {
            OnFetchWorld = (_, _, _) => Task.FromException<MarketScopePrice?>(cancellation),
            OnFetchScope = (_, _, _) => Task.FromException<MarketScopePrice?>(cancellation)
        };
        var dispatcher = new RecordingFrameworkDispatcher();
        using var helper = CreateHelper(fixture.Repository, transport, dispatcher);

        var thrown = await Assert.ThrowsExceptionAsync<OperationCanceledException>(() =>
            helper.GetPriceAsync(ItemId, WorldId, "Aether", ttlMinutes: 10));

        Assert.AreSame(cancellation, thrown);
        Assert.AreEqual(1, transport.Calls.Count);
        Assert.AreEqual(
            "CacheRead",
            string.Join(',', dispatcher.Calls.Select(call => call.Operation)));
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("42")]
    public async Task GetPriceAsync_WithoutDistinctDataCenter_DoesNotIssueSecondRequest(string dataCenterName)
    {
        using var fixture = new RepositoryFixture();
        var fetchedAt = new DateTime(2026, 8, 18, 15, 0, 0, DateTimeKind.Utc);
        var transport = new FakeMarketboardTransport
        {
            OnFetchWorld = (_, _, _) => Task.FromResult<MarketScopePrice?>(
                new(ItemId, "42", 120, "WorldA", 14, fetchedAt)),
            OnFetchScope = (_, _, _) => throw new AssertFailedException("DC request was not expected.")
        };
        var dispatcher = new RecordingFrameworkDispatcher();
        using var helper = CreateHelper(fixture.Repository, transport, dispatcher);

        var result = await helper.GetPriceAsync(
            ItemId, WorldId, dataCenterName, ttlMinutes: 10);

        Assert.AreEqual(
            new MarketPrice(ItemId, 120, 120, "WorldA", 14, fetchedAt),
            result);
        Assert.AreEqual(
            "world:42",
            string.Join(',', transport.Calls.Select(call => $"{call.Kind}:{call.Scope}")));
    }

    [TestMethod]
    public async Task UniversalisTransport_WorldIpc_ExecutesInsideDispatcherAndForwardsToken()
    {
        var dispatcher = new RecordingFrameworkDispatcher();
        var ipcExecutedInsideDispatcher = false;
        using var http = new HttpClient(new ForbiddenHttpMessageHandler());
        using var transport = new UniversalisMarketboardTransport(
            (itemId, worldId) =>
            {
                ipcExecutedInsideDispatcher = dispatcher.IsExecuting;
                Assert.AreEqual(ItemId, itemId);
                Assert.AreEqual(WorldId, worldId);
                return ResponseJson(120, "WorldA", 14);
            },
            http,
            dispatcher);
        using var tokenSource = new CancellationTokenSource();

        var result = await transport.FetchWorldAsync(ItemId, WorldId, tokenSource.Token);

        Assert.IsTrue(ipcExecutedInsideDispatcher);
        Assert.AreEqual(ItemId, result!.ItemId);
        Assert.AreEqual("42", result.Scope);
        Assert.AreEqual(120, result.PricePerUnit);
        Assert.AreEqual("WorldA", result.ServerName);
        Assert.AreEqual(14, result.TotalAvailable);
        Assert.IsTrue(transport.IsIpcAvailable);
        Assert.AreEqual(
            "Ipc",
            string.Join(',', dispatcher.Calls.Select(call => call.Operation)));
        Assert.AreEqual(tokenSource.Token, dispatcher.Calls.Single().Token);
    }

    [TestMethod]
    public async Task UniversalisTransport_WhenWorldIpcFails_FallsBackToRestOutsideDispatcher()
    {
        var dispatcher = new RecordingFrameworkDispatcher();
        var handler = new RecordingHttpMessageHandler(
            dispatcher,
            ResponseJson(120, "WorldA", 14));
        using var http = new HttpClient(handler);
        using var transport = new UniversalisMarketboardTransport(
            (_, _) => throw new InvalidOperationException("IPC unavailable"),
            http,
            dispatcher);
        using var tokenSource = new CancellationTokenSource();
        var before = DateTime.UtcNow;

        var result = await transport.FetchWorldAsync(ItemId, WorldId, tokenSource.Token);

        var after = DateTime.UtcNow;
        Assert.AreEqual(120, result!.PricePerUnit);
        Assert.AreEqual("42", result.Scope);
        Assert.IsTrue(result.CachedAt >= before && result.CachedAt <= after);
        Assert.IsFalse(transport.IsIpcAvailable);
        Assert.AreEqual(1, handler.Calls.Count);
        Assert.IsFalse(handler.Calls.Single().ExecutedInsideDispatcher);
        Assert.IsTrue(handler.Calls.Single().Token.CanBeCanceled);
        Assert.AreEqual(
            "https://universalis.app/api/v2/42/5333?listings=5&entries=0",
            handler.Calls.Single().Uri.ToString());
        Assert.AreEqual(
            "Ipc",
            string.Join(',', dispatcher.Calls.Select(call => call.Operation)));
    }

    [TestMethod]
    public async Task UniversalisTransport_DataCenter_UsesRestWithoutIpcAndForwardsToken()
    {
        var dispatcher = new RecordingFrameworkDispatcher();
        var handler = new RecordingHttpMessageHandler(
            dispatcher,
            ResponseJson(80, "WorldB", 27));
        using var http = new HttpClient(handler);
        using var transport = new UniversalisMarketboardTransport(
            (_, _) => throw new AssertFailedException("IPC was not expected for a data center."),
            http,
            dispatcher);
        using var tokenSource = new CancellationTokenSource();

        var result = await transport.FetchScopeAsync(ItemId, "Aether", tokenSource.Token);

        Assert.AreEqual(80, result!.PricePerUnit);
        Assert.AreEqual("Aether", result.Scope);
        Assert.AreEqual(0, dispatcher.Calls.Count);
        Assert.AreEqual(1, handler.Calls.Count);
        Assert.IsFalse(handler.Calls.Single().ExecutedInsideDispatcher);
        Assert.IsTrue(handler.Calls.Single().Token.CanBeCanceled);
        Assert.AreEqual(
            "https://universalis.app/api/v2/Aether/5333?listings=5&entries=0",
            handler.Calls.Single().Uri.ToString());
    }

    [TestMethod]
    public async Task UniversalisTransport_WhenRestThrowsOperationCanceledException_DoesNotReturnNetworkFailure()
    {
        var dispatcher = new RecordingFrameworkDispatcher();
        using var http = new HttpClient(new CancellationThrowingHttpMessageHandler());
        using var transport = new UniversalisMarketboardTransport(
            invokeIpc: null,
            http,
            dispatcher);

        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() =>
            transport.FetchScopeAsync(ItemId, "Aether", CancellationToken.None));
    }

    private static MarketboardHelper CreateHelper(
        CraftingListRepository repository,
        IMarketboardTransport transport,
        IMarketboardFrameworkDispatcher dispatcher) =>
        new(repository, transport, dispatcher, static _ => false);

    private static DateTime RecentUtc() =>
        DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 1).UtcDateTime;

    private static DateTime StaleUtc() =>
        DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 7200).UtcDateTime;

    private static string ResponseJson(int price, string worldName, int quantity) => $$"""
        {
          "minPrice": {{price}},
          "listings": [
            { "pricePerUnit": {{price}}, "quantity": {{quantity}}, "worldName": "{{worldName}}" }
          ]
        }
        """;

    private sealed class RepositoryFixture : IDisposable
    {
        private readonly string _path = Path.Combine(
            Path.GetTempPath(),
            $"artificer-marketboard-helper-{Guid.NewGuid():N}.db");

        public RepositoryFixture() => Repository = new CraftingListRepository(_path);

        public CraftingListRepository Repository { get; }

        public void Dispose()
        {
            Repository.Dispose();
            SqliteConnection.ClearAllPools();
            File.Delete(_path);
        }
    }

    private sealed record TransportCall(
        string Kind,
        string Scope,
        CancellationToken Token);

    private sealed class FakeMarketboardTransport : IMarketboardTransport
    {
        public Func<uint, uint, CancellationToken, Task<MarketScopePrice?>> OnFetchWorld { get; init; } =
            static (_, _, _) => Task.FromResult<MarketScopePrice?>(null);

        public Func<uint, string, CancellationToken, Task<MarketScopePrice?>> OnFetchScope { get; init; } =
            static (_, _, _) => Task.FromResult<MarketScopePrice?>(null);

        public List<TransportCall> Calls { get; } = [];

        public Task<MarketScopePrice?> FetchWorldAsync(
            uint itemId,
            uint worldId,
            CancellationToken token)
        {
            Calls.Add(new TransportCall("world", worldId.ToString(), token));
            return OnFetchWorld(itemId, worldId, token);
        }

        public Task<MarketScopePrice?> FetchScopeAsync(
            uint itemId,
            string scope,
            CancellationToken token)
        {
            Calls.Add(new TransportCall("scope", scope, token));
            return OnFetchScope(itemId, scope, token);
        }
    }

    private sealed record FrameworkCall(
        MarketboardFrameworkOperation Operation,
        CancellationToken Token,
        bool ExecutedInsideDispatcher)
    {
        public bool ActionCompleted { get; set; }

        public object? CallbackValue { get; set; }
    }

    private sealed class RecordingFrameworkDispatcher : IMarketboardFrameworkDispatcher
    {
        public List<FrameworkCall> Calls { get; } = [];

        public bool IsExecuting { get; private set; }

        public Task<T> RunAsync<T>(
            MarketboardFrameworkOperation operation,
            Func<T> action,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Assert.IsFalse(IsExecuting, "Nested framework dispatch was not expected.");
            IsExecuting = true;
            try
            {
                var call = new FrameworkCall(operation, token, IsExecuting);
                Calls.Add(call);
                var result = action();
                call.CallbackValue = result;
                call.ActionCompleted = true;
                return Task.FromResult(result);
            }
            finally
            {
                IsExecuting = false;
            }
        }
    }

    private sealed record HttpCall(
        Uri Uri,
        CancellationToken Token,
        bool ExecutedInsideDispatcher);

    private sealed class RecordingHttpMessageHandler(
        RecordingFrameworkDispatcher dispatcher,
        string responseJson) : HttpMessageHandler
    {
        public List<HttpCall> Calls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls.Add(new HttpCall(
                request.RequestUri!,
                cancellationToken,
                dispatcher.IsExecuting));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson)
            });
        }
    }

    private sealed class ForbiddenHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new AssertFailedException($"Unexpected HTTP request: {request.RequestUri}");
    }

    private sealed class CancellationThrowingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(
                new OperationCanceledException("HTTP operation cancelled."));
    }
}
