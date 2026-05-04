package com.goldbank.shared.data.remote.grpc

import com.goldbank.shared.data.mapper.AssetMapper
import com.goldbank.shared.data.remote.grpcCall
import com.goldbank.shared.domain.model.AssetDetail
import com.goldbank.shared.domain.model.AssetSummary
import com.goldbank.shared.domain.model.DailyPriceEntry
import com.goldbank.shared.domain.model.PortfolioValue
import com.goldbank.shared.domain.util.Result
import io.grpc.ManagedChannel
import goldbank.v1.assets.AssetServiceGrpcKt.AssetServiceCoroutineStub
import goldbank.v1.assets.AssetServiceOuterClass as Proto

class AssetGrpcClient(channel: ManagedChannel) {

    private val stub = AssetServiceCoroutineStub(channel)

    suspend fun registerAsset(
        customerId: String,
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
            .setCustomerId(customerId)
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

    suspend fun listMyAssets(customerId: String): Result<List<AssetSummary>> = grpcCall {
        val request = Proto.ListMyAssetsRequest.newBuilder()
            .setCustomerId(customerId)
            .build()
        stub.listMyAssets(request).assetsList.map { AssetMapper.toAssetSummary(it) }
    }

    suspend fun getAssetDetail(customerId: String, assetId: String): Result<AssetDetail> = grpcCall {
        val request = Proto.GetAssetDetailRequest.newBuilder()
            .setAssetId(assetId)
            .setCustomerId(customerId)
            .build()
        AssetMapper.toAssetDetail(stub.getAssetDetail(request))
    }

    suspend fun requestRelease(
        customerId: String,
        assetId: String,
        reason: String,
    ): Result<Boolean> = grpcCall {
        val request = Proto.RequestAssetReleaseRequest.newBuilder()
            .setAssetId(assetId)
            .setCustomerId(customerId)
            .setReason(reason)
            .build()
        stub.requestAssetRelease(request).success
    }

    suspend fun getPortfolioValue(customerId: String): Result<PortfolioValue> = grpcCall {
        val request = Proto.GetPortfolioValueRequest.newBuilder()
            .setCustomerId(customerId)
            .build()
        AssetMapper.toPortfolioValue(stub.getPortfolioValue(request))
    }

    suspend fun getDailyPrices(): Result<List<DailyPriceEntry>> = grpcCall {
        val request = Proto.GetDailyPricesRequest.newBuilder().build()
        stub.getDailyPrices(request).pricesList.map { AssetMapper.toDailyPriceEntry(it) }
    }
}
