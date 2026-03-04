package com.unibank.shared.domain.repository

import com.unibank.shared.domain.model.BrandingConfig
import com.unibank.shared.domain.util.Result

interface BrandingRepository {
    suspend fun getBranding(tenantId: String): Result<BrandingConfig>
}
