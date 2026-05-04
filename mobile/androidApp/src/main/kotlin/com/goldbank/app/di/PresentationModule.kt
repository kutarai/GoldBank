package com.goldbank.app.di

import com.goldbank.app.viewmodel.AgentViewModel
import com.goldbank.app.viewmodel.AssetViewModel
import com.goldbank.app.viewmodel.AuthViewModel
import com.goldbank.app.viewmodel.BillPayViewModel
import com.goldbank.app.viewmodel.BrandingViewModel
import com.goldbank.app.viewmodel.ChatViewModel
import com.goldbank.app.viewmodel.DisputeViewModel
import com.goldbank.app.viewmodel.DocumentScanViewModel
import com.goldbank.app.viewmodel.EkubViewModel
import com.goldbank.app.viewmodel.FraudAlertViewModel
import com.goldbank.app.viewmodel.HomeViewModel
import com.goldbank.app.viewmodel.KycViewModel
import com.goldbank.app.viewmodel.LoanViewModel
import com.goldbank.app.viewmodel.MerchantViewModel
import com.goldbank.app.viewmodel.PaymentViewModel
import com.goldbank.app.viewmodel.ProfileViewModel
import com.goldbank.app.viewmodel.SecurityViewModel
import com.goldbank.app.viewmodel.TransferViewModel
import org.koin.core.module.dsl.viewModel
import org.koin.dsl.module

val presentationModule = module {
    viewModel { BrandingViewModel() }
    viewModel { AuthViewModel(get(), get(), get(), get(), get(), get(), get(), get()) }
    viewModel { HomeViewModel(get(), get(), get(), get(), get(), get(), get()) }
    viewModel { PaymentViewModel(get(), get(), get()) }
    viewModel { TransferViewModel(get(), get()) }
    viewModel { BillPayViewModel(get(), get()) }
    viewModel { AgentViewModel(get(), get()) }
    viewModel { LoanViewModel(get(), get(), get()) }
    viewModel { KycViewModel(get(), get(), get()) }
    viewModel { MerchantViewModel(get(), get()) }
    viewModel { ProfileViewModel(get(), get(), get()) }
    viewModel { SecurityViewModel(get(), get()) }
    viewModel { ChatViewModel(get(), get()) }
    viewModel { DocumentScanViewModel(get(), get()) }
    viewModel { DisputeViewModel(get(), get(), get()) }
    viewModel { FraudAlertViewModel(get(), get()) }
    viewModel { AssetViewModel(get(), get(), get()) }
    viewModel { EkubViewModel(get(), get()) }
}
