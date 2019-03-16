<div>
    <div class="slds-page-header">
        <div class="slds-page-header__row">
            <div class="slds-page-header__col-title">
                <div class="slds-media">
                    <div class="slds-media__figure">
                        <span class="slds-icon_container slds-icon-standard-custom" title="Setup">
                            <svg class="slds-icon slds-page-header__icon">
                                <use xmlns:xlink="http://www.w3.org/1999/xlink" xlink:href="/assets/icons/standard-sprite/svg/symbols.svg#custom" />
                            </svg>
                        </span>
                    </div>
                    <div class="slds-media__body">
                        <div class="slds-page-header__name">
                            <div class="slds-page-header__name-title">
                                <h1>
                                    <span class="slds-page-header__title slds-truncate" title="Setup">Setup</span>
                                </h1>
                            </div>
                        </div><!--<p class="slds-page-header__name-meta">-</p>-->
                    </div>
                </div>
            </div>
        </div>
    </div>
    <div style="padding:1em;">
        <div class="slds-form-element">
            <div class="slds-clearfix">
                <label class="slds-form-element__label" for="text-input-id-1">Org settings file path</label>
            </div>
            <div class="slds-form-element__control" style="margin-top:0.1em;">
                <v-combobox v-model="orgSettingsPath" :items="orgSettingsPathItems" solo dense cache-items hide-no-data no-filter
                                id="text-input-id-1" :search-input.sync="fetchOrgSettingsPath"></v-combobox>
            </div>
        </div>
        <div class="slds-form-element">
            <div class="slds-clearfix">
                <label class="slds-form-element__label" for="text-input-id-2">Chrome path</label>
            </div>
            <div class="slds-form-element__control" id="text-input-id-2" style="margin-top:0.1em;">
                <v-combobox v-model="chromePath" :items="chromePathItems" solo dense cache-items hide-no-data no-filter
                                id="text-input-id-2" :search-input.sync="fetchChromePath"></v-combobox>
            </div>
        </div>
        <hr />
        <div class="slds-form-element">
            <button class="slds-button slds-button_success" @click="dispatch('saveConfig',{orgSettingsPath,chromePath})">Save path</button>
        </div>
    </div>
</div>