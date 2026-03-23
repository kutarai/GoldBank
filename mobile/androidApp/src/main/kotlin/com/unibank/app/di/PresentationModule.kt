package com.unibank.app.di

import com.unibank.app.viewmodel.AgentViewModel
import com.unibank.app.viewmodel.AuthViewModel
import com.unibank.app.viewmodel.BillPayViewModel
import com.unibank.app.viewmodel.BrandingViewModel
import com.unibank.app.viewmodel.ChatViewModel
import com.unibank.app.viewmodel.DisputeViewModel
import com.unibank.app.viewmodel.DocumentScanViewModel
import com.unibank.app.viewmodel.FraudAlertViewModel
import com.unibank.app.viewmodel.HomeViewModel
import com.unibank.app.viewmodel.KycViewModel
import com.unibank.app.viewmodel.LoanViewModel
import com.unibank.app.viewmodel.MerchantViewModel
import com.unibank.app.viewmodel.PaymentViewModel
import com.unibank.app.viewmodel.ProfileViewModel
import com.unibank.app.viewmodel.SecurityViewModel
import com.unibank.app.viewmodel.TransferViewModel
import org.koin.core.module.dsl.viewModel
import org.koin.dsl.module

val presentationModule = module {
    viewModel { BrandingViewModel() }
    viewModel { AuthViewModel(get(), get(), get(), get(), get(), get(), get(), get()) }
    viewModel { HomeViewModel(get(), get(), get(), get(), get(), get()) }
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
}
