<header class="slds-global-header_container">
    <div class="slds-global-header slds-grid slds-grid_align-spread">
        <div class="slds-global-header__item">
            <img src="/favicon.ico" height="40" />
        </div>
        <div class="slds-global-header__item">
            <ul class="slds-global-actions">
                <li class="slds-global-actions__item">
                    <button class="slds-box slds-theme_default slds-global-actions__setup slds-global-actions__item-action" title="Manage Organization" @click="dispatch('showOrgModal',true)" style="padding:0.5em;white-space:nowrap">
                        Manage Organizations
                        <svg class="slds-button__icon slds-global-header__icon">
                            <use xlink:href="/assets/icons/standard-sprite/svg/symbols.svg#picklist_choice"></use>
                        </svg>
                    </button>
                </li>
            </ul>
        </div>
        <div class="slds-global-header__item">
            <h1 v-if="currentInstanceUrl">
                <a href="javascript:void(0)" @click="dispatch('viewPage',currentInstanceUrl)">
                    {{currentInstanceUrl|orgname}}
                </a>
            </h1>
        </div>
        <div class="slds-global-header__item slds-global-header__item_search" style="flex-grow:1;position:relative;">
            <section :class="['slds-popover','slds-nubbin_top-right',globalSearch?'':'slds-popover_hide']" role="dialog" style="position:absolute;top:3.4em;right:-0.8em;">
                <button class="slds-button slds-button_icon slds-button_icon-small slds-float_right slds-popover__close" title="Close dialog"
                        @click="dispatch('globalSearch',null)">
                    <svg class="slds-button__icon">
                        <use xmlns:xlink="http://www.w3.org/1999/xlink" xlink:href="/assets/icons/utility-sprite/svg/symbols.svg#close" />
                    </svg>
                    <span class="slds-assistive-text">Close dialog</span>
                </button>
                <div class="slds-popover__body" id="dialog-body-id-6">
					<span class="slds-badge" v-if="objectType!='data'">{{objectType}}</span>
                    <div>Label: {{objectLabel}}</div>
                    <div>Name: {{objectName}}</div>
                    <div v-if="objectType=='data'&&objectPrefix">Prefix: {{objectPrefix}}</div>
                    <div v-if="objectType=='data'&&objectOverviewPage"><a href="javascript:void(0)" @click="dispatch('viewPage',objectOverviewPage)">Overview page</a></div>
                    <div v-if="objectType=='data'&&objectListPage"><a href="javascript:void(0)" @click="dispatch('viewPage',objectListPage)">List page</a></div>
                    <div v-if="objectType=='data'&&objectSetupPage"><a href="javascript:void(0)" @click="dispatch('viewPage',objectSetupPage)">Setup page</a></div>
                </div>
            </section>
            <v-autocomplete v-model="globalSearch" :items="globalSearchItems" dense :filter="globalSearchFilter"
                            :clearable="true" placeholder="Record id, id prefix, object name or url">

                <template #selection="data">
                    <template v-if="typeof data.item !== 'object'">
                        {{data.item}}
                    </template>
                    <template v-else>
                        <b style="font-weight:bold;display:inline-block;margin-right:10px;">{{data.item.keyPrefix|empty('---')}}</b>
                        <span style="display:inline-block;margin-right:10px;">{{data.item.label}}</span>
                        <span style="color:#ccc;display:inline-block">{{data.item.name}} <span class="slds-badge" v-if="data.item.type!='data'">{{data.item.type}}</span></span>
                    </template>
                </template>
                <template #item="data">
                    <template v-if="typeof data.item !== 'object'">
                        <v-list-tile-content v-text="data.item"></v-list-tile-content>
                    </template>
                    <template v-else>
                        <v-list-tile-action>
                            <b>{{data.item.keyPrefix|empty('---')}}</b>
                        </v-list-tile-action>
                        <v-list-tile-content>
                            <v-list-tile-title>{{data.item.label}}</v-list-tile-title>
                            <v-list-tile-sub-title>{{data.item.name}} <span class="slds-badge" v-if="data.item.type!='data'">{{data.item.type}}</span></v-list-tile-sub-title>
                        </v-list-tile-content>
                    </template>
                </template>
            </v-autocomplete>
        </div>
        <div class="slds-global-header__item">
            <a class="slds-box slds-theme_default slds-global-actions__setup slds-global-actions__item-action"
                title="GitHub Page" style="padding:0.5em;white-space:nowrap" href="https://github.com/ste80/sf-dataexport" target="_blank">
                GitHub Page
                <svg class="slds-button__icon slds-global-header__icon">
                    <use xlink:href="/assets/icons/standard-sprite/svg/symbols.svg#question_best"></use>
                </svg>
            </a>
        </div>
        <div class="slds-global-header__item" v-if="currentInstanceUrl" style="padding-right:0;">
            <h1>
                <a href="javascript:void(0" @click="dispatch('showUserPopover',true)">
                    {{userDisplayName}} {{userEmail}}
                </a>
            </h1>
        </div>
        <div class="slds-global-header__item" v-if="currentInstanceUrl" style="padding-left:0;">
            <div class="slds-dropdown-trigger slds-dropdown-trigger_click">
                <section :class="['slds-popover','slds-nubbin_top-right',showUserPopover?'':'slds-popover_hide']" role="dialog" style="position:absolute;top:3.2em;right:-0.8em;">
                    <button class="slds-button slds-button_icon slds-button_icon-small slds-float_right slds-popover__close" title="Close"
                            @click="dispatch('showUserPopover',false)">
                        <svg class="slds-button__icon">
                            <use xmlns:xlink="http://www.w3.org/1999/xlink" xlink:href="/assets/icons/utility-sprite/svg/symbols.svg#close" />
                        </svg>
                        <span class="slds-assistive-text">Close</span>
                    </button>
                    <div class="slds-popover__body" id="dialog-body-id-6">
                        <div :style="{visibility:!userItems.length?'visible':'hidden'}">
                            <spinner class="slds-spinner_x-small" style="top:3em"></spinner>
                        </div>
                        <div :class="[userItems.length?'':'hidden']">
                            <v-autocomplete v-model="popoverUserId" :auto-select-first="true" :items="userItems" solo dense
                                            :clearable="true" placeholder="&lt;select salesforce user&gt;"></v-autocomplete>
                            <div class="slds-button-group" role="group">
                                <button class="slds-button slds-button_neutral"
                                        @click="dispatch('switchUser',{instanceUrl:currentInstanceUrl,userId:popoverUserId})"
                                        :disabled="!popoverUserId">
                                    Switch User
                                </button>
                                <button class="slds-button slds-button_neutral"
                                        @click="dispatch('loginAsUser',{instanceUrl:currentInstanceUrl,userId:popoverUserId})"
                                        :disabled="!popoverUserId">
                                    Login As
                                </button>
                                <a class="slds-button slds-button_neutral" href="javascript:void(0)" @click="dispatch('viewPage',currentInstanceUrl+'/'+popoverUserId+'?noredirect=1')"
                                    :disabled="!popoverUserId">
                                    View
                                </a>
                            </div>
                            <div>
                                {{userRoleName}} <span v-if="userProfileName">({{userProfileName}})</span> &nbsp;
                            </div>
                            <div v-if="userPicture">
                                <img :src="userPicture" />
                            </div>
                        </div>
                    </div>
                </section>
                <button class="slds-button slds-global-actions__avatar slds-global-actions__item-action" @click="dispatch('showUserPopover',true)">
                    <span class="slds-avatar slds-avatar_circle slds-avatar_medium">
                        <img :alt="userName" :src="userThumbnail|empty('/assets/images/avatar2.jpg')" :title="userName" />
                    </span>
                </button>
            </div>
        </div>
    </div>
</header>