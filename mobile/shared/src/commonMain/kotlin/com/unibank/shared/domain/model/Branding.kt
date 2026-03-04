package com.unibank.shared.domain.model

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
            appName = "UniBank",
            logoUrl = "",
            primaryColor = "#1B5E20",
            secondaryColor = "#FFD600",
            accentColor = "#00C853",
            faviconUrl = "",
            supportEmail = "support@unibank.co.zw",
            supportPhone = "+263771000000",
        )
    }
}
