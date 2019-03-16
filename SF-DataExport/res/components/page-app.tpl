<v-app>
    <spinner class="slds-spinner_medium" v-if="isLoading"></spinner>
    <div class="slds-backdrop slds-backdrop_open" v-if="isLoading"></div>

    <page-header></page-header>

    <div style="margin-top:3.5rem;">
        <div class="slds-tabs_default">
            <ul class="slds-tabs_default__nav" role="tablist">
                <li :class="['slds-tabs_default__item',tab=='overview'?'slds-is-active':'']" title="Overview">
                    <a class="slds-tabs_default__link" href="javascript:void(0);" role="tab" tabindex="0" @click="dispatch('tab','overview')">
                        <span class="slds-tabs__left-icon">
                            <span class="slds-icon_container slds-icon-standard-strategy" title="Overview">
                                <svg class="slds-icon slds-icon_small">
                                    <use xlink:href="/assets/icons/standard-sprite/svg/symbols.svg#strategy"></use>
                                </svg>
                            </span>
                        </span>Overview
                    </a>
                </li>
                <li :class="['slds-tabs_default__item',tab=='photos'?'slds-is-active':'']" title="User Photos">
                    <a class="slds-tabs_default__link" href="javascript:void(0);" role="tab" tabindex="0" @click="dispatch('tab','photos')">
                        <span class="slds-tabs__left-icon">
                            <span class="slds-icon_container slds-icon-standard-carousel" title="User Photos">
                                <svg class="slds-icon slds-icon_small">
                                    <use xlink:href="/assets/icons/standard-sprite/svg/symbols.svg#carousel"></use>
                                </svg>
                            </span>
                        </span>User Photos
                    </a>
                </li>
                <li :class="['slds-tabs_default__item',tab=='limits'?'slds-is-active':'']" title="Org Limits">
                    <a class="slds-tabs_default__link" href="javascript:void(0);" role="tab" tabindex="0" @click="dispatch('tab','limits')">
                        <span class="slds-tabs__left-icon">
                            <span class="slds-icon_container slds-icon-standard-poll" title="Org Limits">
                                <svg class="slds-icon slds-icon_small">
                                    <use xlink:href="/assets/icons/standard-sprite/svg/symbols.svg#poll"></use>
                                </svg>
                            </span>
                        </span>Org Limits
                    </a>
                </li>
                <li :class="['slds-tabs_default__item',tab=='data'?'slds-is-active':'']" title="Data Import/Export">
                    <a class="slds-tabs_default__link" href="javascript:void(0);" role="tab" tabindex="0" @click="dispatch('tab','data')">
                        <span class="slds-tabs__left-icon">
                            <span class="slds-icon_container slds-icon-standard-datadotcom" title="Data Import/Export">
                                <svg class="slds-icon slds-icon_small">
                                    <use xlink:href="/assets/icons/standard-sprite/svg/symbols.svg#datadotcom"></use>
                                </svg>
                            </span>
                        </span>Data Import/Export
                    </a>
                </li>
                <li :class="['slds-tabs_default__item',tab=='downloaddataexport'?'slds-is-active':'']" title="Download Data Export">
                    <a class="slds-tabs_default__link" href="javascript:void(0);" role="tab" tabindex="0" @click="dispatch('tab','downloaddataexport')">
                        <span class="slds-tabs__left-icon">
                            <span class="slds-icon_container slds-icon-standard-folder" title="Download Data Export">
                                <svg class="slds-icon slds-icon_small">
                                    <use xlink:href="/assets/icons/standard-sprite/svg/symbols.svg#folder"></use>
                                </svg>
                            </span>
                        </span>Download Data Export
                    </a>
                </li>
                <li :class="['slds-tabs_default__item',tab=='setup'?'slds-is-active':'']" title="Setup">
                    <a class="slds-tabs_default__link" href="javascript:void(0);" role="tab" tabindex="0" @click="dispatch('tab','setup')">
                        <span class="slds-tabs__left-icon">
                            <span class="slds-icon_container slds-icon-standard-custom" title="Setup">
                                <svg class="slds-icon slds-icon_small">
                                    <use xlink:href="/assets/icons/standard-sprite/svg/symbols.svg#custom"></use>
                                </svg>
                            </span>
                        </span>Setup
                    </a>
                </li>
            </ul>
            <div :class="['slds-tabs_default__content',tab=='overview'?'slds-show':'slds-hide']" style="padding:1em;">
                <overview-tab></overview-tab>
            </div>
            <div :class="['slds-tabs_default__content',tab=='photos'?'slds-show':'slds-hide']" style="padding:1em;">
                <photos-tab></photos-tab>
            </div>
            <div :class="['slds-tabs_default__content',tab=='limits'?'slds-show':'slds-hide']" style="padding:1em;">
                <limits-tab></limits-tab>
            </div>
            <div :class="['slds-tabs_default__content',tab=='data'?'slds-show':'slds-hide']" style="padding:1em;">
                <data-tab></data-tab>
            </div>
            <div :class="['slds-tabs_default__content',tab=='downloaddataexport'?'slds-show':'slds-hide']" style="padding:1em;">
                <download-dataexport-tab></download-dataexport-tab>
            </div>
            <div :class="['slds-tabs_default__content',tab=='setup'?'slds-show':'slds-hide']" style="padding:1em;">
                <setup-tab></setup-tab>
            </div>
        </div>
    </div>

    <organization-modal v-if="showOrgModal"></organization-modal>

    <v-modal @close="dispatch('alertMessage','')" v-if="alertMessage">
        <div class="slds-notify slds-notify_alert slds-theme_alert-texture slds-theme_warning">
            <span class="slds-assistive-text">warning</span>
            <span class="slds-icon_container slds-icon-utility-warning slds-m-right_x-small" title="Description of icon when needed">
                <svg class="slds-icon slds-icon_x-small">
                    <use xlink:href="/assets/icons/utility-sprite/svg/symbols.svg#warning"></use>
                </svg>
            </span>
            <h2>{{alertMessage}}</h2>
        </div>
        <template #close-text>
            OK
        </template>
    </v-modal>
</v-app>