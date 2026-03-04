package com.unibank.app.viewmodel

import androidx.lifecycle.ViewModel
import com.unibank.shared.domain.model.BrandingConfig
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

class BrandingViewModel : ViewModel() {

    private val _branding = MutableStateFlow(BrandingConfig.DEFAULT)
    val branding: StateFlow<BrandingConfig> = _branding.asStateFlow()

    // Will be connected to WhiteLabelService.GetBranding RPC in a later phase
    // For now, uses default branding
}
