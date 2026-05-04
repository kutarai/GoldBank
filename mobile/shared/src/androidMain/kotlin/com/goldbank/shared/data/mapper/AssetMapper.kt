package com.goldbank.shared.data.mapper

import com.goldbank.shared.domain.model.AssetDetail
import com.goldbank.shared.domain.model.AssetSummary
import com.goldbank.shared.domain.model.AssetTypeSummary
import com.goldbank.shared.domain.model.DailyPriceEntry
import com.goldbank.shared.domain.model.PortfolioValue
import com.goldbank.shared.domain.model.ValuationEntry
import goldbank.v1.assets.AssetServiceOuterClass as Proto

object AssetMapper {

    fun toAssetSummary(proto: Proto.AssetResponse): AssetSummary {
        val value = proto.currentValue
        return AssetSummary(
            assetId = proto.id,
            receiptNumber = proto.receiptNumber,
            assetType = proto.assetType,
            description = proto.description,
            quantity = proto.quantity.toDoubleOrNull() ?: 0.0,
            unit = proto.unit,
            currentValueAmount = value?.amount ?: "0",
            currentValueCurrency = value?.currency?.ifEmpty { "USD" } ?: "USD",
            verificationStatus = proto.verificationStatus,
            status = proto.status,
            depositHouseName = proto.depositHouseName,
            receiptDate = proto.receiptDate?.seconds?.times(1000) ?: 0L,
        )
    }

    fun toAssetDetail(proto: Proto.AssetDetailResponse): AssetDetail {
        val value = proto.currentValue
        val house = proto.depositHouse
        return AssetDetail(
            assetId = proto.id,
            receiptNumber = proto.receiptNumber,
            assetType = proto.assetType,
            description = proto.description,
            quantity = proto.quantity.toDoubleOrNull() ?: 0.0,
            unit = proto.unit,
            weightGrams = proto.weightGrams.takeIf { it.isNotEmpty() }?.toDoubleOrNull(),
            purity = proto.purity.takeIf { it.isNotEmpty() }?.toDoubleOrNull(),
            currentValueAmount = value?.amount ?: "0",
            currentValueCurrency = value?.currency?.ifEmpty { "USD" } ?: "USD",
            verificationStatus = proto.verificationStatus,
            status = proto.status,
            depositHouseName = house?.name ?: "",
            depositHouseAddress = buildString {
                if (house != null) {
                    append(house.address)
                    if (house.city.isNotEmpty()) append(", ${house.city}")
                }
            },
            receiptDate = proto.receiptDate?.seconds?.times(1000) ?: 0L,
            valuations = proto.valuationsList.map { toValuationEntry(it) },
        )
    }

    fun toValuationEntry(proto: Proto.ValuationEntry): ValuationEntry {
        val money = proto.amount
        return ValuationEntry(
            amount = money?.amount ?: "0",
            currency = money?.currency?.ifEmpty { "USD" } ?: "USD",
            valuerName = proto.valuerName,
            date = proto.createdAt?.seconds?.times(1000) ?: 0L,
        )
    }

    fun toPortfolioValue(proto: Proto.PortfolioValueResponse): PortfolioValue {
        val zwg = proto.totalValueZwg
        val usd = proto.totalValueUsd
        return PortfolioValue(
            totalValueZwg = zwg?.amount ?: "0",
            totalValueUsd = usd?.amount ?: "0",
            assetsByType = proto.assetsByTypeList.map { toAssetTypeSummary(it) },
        )
    }

    fun toAssetTypeSummary(proto: Proto.AssetTypeSummary): AssetTypeSummary {
        val total = proto.totalValue
        return AssetTypeSummary(
            assetType = proto.assetType,
            count = proto.count,
            totalValueUsd = total?.amount ?: "0",
        )
    }

    fun toDailyPriceEntry(proto: Proto.DailyPriceEntry): DailyPriceEntry = DailyPriceEntry(
        assetType = proto.assetType,
        pricePerGramUsd = proto.pricePerGramUsd.toDoubleOrNull() ?: 0.0,
        pricePerOzUsd = proto.pricePerOzUsd.toDoubleOrNull() ?: 0.0,
        date = proto.date,
        source = proto.source,
    )
}
