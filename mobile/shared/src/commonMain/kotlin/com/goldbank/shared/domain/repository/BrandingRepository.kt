package com.goldbank.shared.domain.repository

import com.goldbank.shared.domain.model.BrandingConfig
import com.goldbank.shared.domain.util.Result

interface BrandingRepository {
    suspend fun getBranding(tenantId: String): Result<BrandingConfig>
}
