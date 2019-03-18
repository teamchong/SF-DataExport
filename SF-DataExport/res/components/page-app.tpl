<v-app>
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
	
    <section :class="['slds-popover','slds-nubbin_top-right',showUserPopover?'':'slds-popover_hide']" style="position:fixed;top:3.2em;right:14.2em;width:450px;z-index:99999">
        <button class="slds-button slds-button_icon slds-button_icon-small slds-float_right slds-popover__close" title="Close"
                @click="dispatch('showUserPopover',false)">
            <svg class="slds-button__icon">
                <use xmlns:xlink="http://www.w3.org/1999/xlink" xlink:href="/assets/icons/utility-sprite/svg/symbols.svg#close" />
            </svg>
        </button>
        <div class="slds-popover__body">
            <div :style="{visibility:!userItems.length?'visible':'hidden'}">
                <spinner class="slds-spinner slds-spinner_x-small" style="top:3em;"></spinner>
            </div>
            <div :class="[userItems.length?'':'hidden']">
                <a class="slds-button slds-button_neutral" href="javascript:void(0)" @click="dispatch('viewPage',currentInstanceUrl+'/'+popoverUserId+'?noredirect=1')"
                    :disabled="!popoverUserId">
                    View Setup Page
                </a>
                <a class="slds-button slds-button_neutral" href="javascript:void(0)" @click="dispatch('viewPage',currentInstanceUrl+'/_ui/core/userprofile/UserProfilePage?u='+popoverUserId+'&tab=sfdc.ProfilePlatformOverview')"
                    :disabled="!popoverUserId">
                    View Profile Page
                </a>
                <!--<button class="slds-button slds-button_neutral"
                        @click="dispatch('switchUser',{instanceUrl:currentInstanceUrl,userId:popoverUserId})"
                        :disabled="!popoverUserId">
                    Switch User
                </button>-->
				<div class="slds-box" style="margin-top:0.1em;margin-bottom:0.1em;">
					<button class="slds-button slds-button_neutral"
							@click="dispatch('loginAsUser',{instanceUrl:currentInstanceUrl,userId:popoverUserId})"
							:disabled="!popoverUserId" style="width:100%">
						Login As
					</button>
					<cmdcopy-element :label="'Command line for Login As '+userDisplayName" :cmd="cmdLoginAs"></cmdcopy-element>
				</div>
                <div>
                    <b>{{userDisplayName}}</b> ({{userName}})
                </div>
                <div>
                    {{userRoleName}} <span v-if="userProfileName">({{userProfileName}})</span> &nbsp;
                </div>
				<div v-if="userEmail">
					<a :href="'mailto:'+userEmail">{{userEmail}}</a>
				</div>
                <div v-if="userPicture">
                    <img :src="userPicture" />
                </div>
            </div>
        </div>
    </section>

    <organization-modal v-if="showOrgModal"></organization-modal>

    <v-modal @close="dispatch('alertMessage','')" v-if="alertMessage">
        <div class="slds-notify slds-notify_alert slds-theme_alert-texture slds-theme_warning">
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
	
    <spinner class="slds-spinner slds-spinner_medium" v-if="isLoading"></spinner>
    <div class="slds-backdrop slds-backdrop_open" v-if="isLoading"></div>
</v-app>