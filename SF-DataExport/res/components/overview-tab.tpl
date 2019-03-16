﻿<div>
    <div class="slds-page-header">
        <div class="slds-page-header__row">
            <div class="slds-page-header__col-title">
                <div class="slds-media">
                    <div class="slds-media__figure">
                        <span class="slds-icon_container slds-icon-standard-strategy" title="Overview">
                            <svg class="slds-icon slds-page-header__icon">
                                <use xmlns:xlink="http://www.w3.org/1999/xlink" xlink:href="/assets/icons/standard-sprite/svg/symbols.svg#strategy" />
                            </svg>
                        </span>
                    </div>
                    <div class="slds-media__body">
                        <div class="slds-page-header__name">
                            <div class="slds-page-header__name-title">
                                <h1>
                                    <span class="slds-page-header__title slds-truncate" title="Overview">Overview</span>
                                </h1>
                            </div>
                        </div><!--<p class="slds-page-header__name-meta">-</p>-->
                    </div>
                </div>
            </div>
        </div>
    </div>
    <div style="padding:1em;position:relative;">
        <div class="slds-form-element" v-if="currentInstanceUrl && data.name">
            <div class="slds-form-element__control slds-input-has-icon slds-input-has-icon_right">
                <input placeholder="Search User" class="slds-input" type="text" v-model="orgChartSearch" />
                <button class="slds-button slds-button_icon slds-input__icon slds-input__icon_right" title="Reset" @click="dispatch('orgChartSearch',' ');dispatch('orgChartSearch','')">
                    <svg class="slds-button__icon slds-icon-text-light">
                        <use xmlns:xlink="http://www.w3.org/1999/xlink" xlink:href="/assets/icons/action-sprite/svg/symbols.svg#reset_password" />
                    </svg>
                </button>
            </div>
        </div>
        <org-chart id="chart-container" :data="data" v-if="currentInstanceUrl && data.name"></org-chart>
        <spinner class="slds-spinner_medium" style="margin-top:2em;" v-if="currentInstanceUrl && !data.name"></spinner>
        <div v-if="!currentInstanceUrl" style="padding:5em;">
            <a href="javascript:void(0)" @click="dispatch('showOrgModal',true)">click here to login your organization.</a>
        </div>
    </div>
</div>