package com.unibank.shared.data.remote.grpc

import com.unibank.shared.data.mapper.AssetMapper
import com.unibank.shared.data.remote.grpcCall
import com.unibank.shared.domain.model.AssetDetail
import com.unibank.shared.domain.model.AssetSummary
import com.unibank.shared.domain.model.DailyPriceEntry
import com.unibank.shared.domain.model.PortfolioValue
import com.unibank.shared.domain.util.Result
import io.grpc.ManagedChannel
import unibank.v1.assets.AssetServiceGrpcKt.AssetServiceCoroutineStub
import unibank.v1.assets.AssetServiceOuterClass as Proto

class AssetGrpcClient(channel: ManagedChannel) {

    private val stub = AssetServiceCoroutineStub(channel)

    suspend fun registerAsset(
        accountId: String,
        receiptNumber: String,
        assetType: String,
        description: String,
        quantity: String,
        unit: String,
        weightGrams: String,
        purity: String,
        receiptImagePath: String,
        depositHouseId: String,
    ): Result<AssetSummary> = grpcCall {
        val request = Proto.RegisterAssetRequest.newBuilder()
            .setAccountId(accountId)
            .setReceiptNumber(receiptNumber)
            .setAssetType(assetType)
            .setDescription(description)
            .setQuantity(quantity)
            .setUnit(unit)
            .setWeightGrams(weightGrams)
            .setPurity(purity)
            .setReceiptImagePath(receiptImagePath)
            .setDepositHouseId(depositHouseId)
            .build()
        AssetMapper.toAssetSummary(stub.registerAsset(request))
    }

    suspend fun listMyAssets(accountId: String): Result<List<AssetSummary>> = grpcCall {
        val request = Proto.ListMyAssetsRequest.newBuilder()
            .setAccountId(accountId)
            .build()
        stub.listMyAssets(request).assetsList.map { AssetMapper.toAssetSummary(it) }
    }

    suspend fun getAssetDetail(accountId: String, assetId: String): Result<AssetDetail> = grpcCall {
        val request = Proto.GetAssetDetailRequest.newBuilder()
            .setAssetId(assetId)
            .setAccountId(accountId)
            .build()
        AssetMapper.toAssetDetail(stub.getAssetDetail(request))
    }

    suspend fun requestRelease(
        accountId: String,
        assetId: String,
        reason: String,
    ): Result<Boolean> = grpcCall {
        val request = Proto.RequestAssetReleaseRequest.newBuilder()
            .setAssetId(assetId)
            .setAccountId(accountId)
            .setReason(reason)
            .build()
        stub.requestAssetRelease(request).success
    }

    suspend fun getPortfolioValue(accountId: String): Result<PortfolioValue> = grpcCall {
        val request = Proto.GetPortfolioValueRequest.newBuilder()
            .setAccountId(accountId)
            .build()
        AssetMapper.toPortfolioValue(stub.getPortfolioValue(request))
    }

    suspend fun getDailyPrices(): Result<List<DailyPriceEntry>> = grpcCall {
        val request = Proto.GetDailyPricesRequest.newBuilder().build()
        stub.getDailyPrices(request).pricesList.map { AssetMapper.toDailyPriceEntry(it) }
    }
}
