package com.goldbank.shared.domain.model

data class BrandingConfig(
    val tenantId: String,
    val appName: String,
    val logoUrl: String,
    val primaryColor: String,
    val secondaryColor: String,
    val accentColor: String,
    val faviconUrl: String,
    val supportEmail: String,
    val supportPhone: String,
) {
    companion object {
        val DEFAULT = BrandingConfig(
            tenantId = "default",
            appName = "GoldBank",
            logoUrl = "",
            primaryColor = "#C9A227",
            secondaryColor = "#3D2E12",
            accentColor = "#FFB300",
            faviconUrl = "",
            supportEmail = "support@goldbank.co.zw",
            supportPhone = "+263771000000",
        )
    }
}
