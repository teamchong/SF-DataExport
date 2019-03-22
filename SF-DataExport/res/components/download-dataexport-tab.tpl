<div>
    <div class="slds-page-header">
        <div class="slds-page-header__row">
            <div class="slds-page-header__col-title">
                <div class="slds-media">
                    <div class="slds-media__figure">
                        <span class="slds-icon_container slds-icon-standard-folder" title="Download Data Export">
                            <svg class="slds-icon slds-page-header__icon">
                                <use xmlns:xlink="http://www.w3.org/1999/xlink" xlink:href="/assets/icons/standard-sprite/svg/symbols.svg#folder" />
                            </svg>
                        </span>
                    </div>
                    <div class="slds-media__body">
                        <div class="slds-page-header__name">
                            <div class="slds-page-header__name-title">
                                <h1>
                                    <span class="slds-page-header__title slds-truncate" title="Download Data Export">Download Data Export</span>
                                </h1>
                            </div>
                        </div><!--<p class="slds-page-header__name-meta">-</p>-->
                    </div>
                </div>
            </div>
        </div>
    </div>
    <div v-if="currentInstanceUrl" style="padding:1em;">

        <p>&nbsp;</p>
        <p>&nbsp;</p>

        <div class="slds-form-element">
            <div class="slds-clearfix">
                <label class="slds-form-element__label" for="text-input-id-1">Email to (Optional, only send after completed/failed)</label>
            </div>
            <div class="slds-form-element__control" style="margin-top:0.1em;">
                <div>
                    <input class="slds-input" type="text" v-model="exportEmails" />
                </div>
            </div>
        </div>

        <p>&nbsp;</p>

		<dir-element v-model="exportPath" label="Export to"></dir-element>

		<cmdcopy-element label="Command line for Download Export Data" :cmd="cmdExport"></cmdcopy-element>

        <hr />
        <div class="slds-form-element">
            <button class="slds-button slds-button_success" @click="dispatch('ViewDownloadExports', {exportPath,instanceUrl:currentInstanceUrl,userId:userIdAs})">
                View Page
            </button>

            <button class="slds-button slds-button_success" @click="dispatch('DownloadExports', {exportPath,instanceUrl:currentInstanceUrl,exportEmails})">
                Download Now
            </button>
        </div>


        <div v-if="exportResult">
            <hr />
            <pre>{{exportResult}}</pre>
            <div>
                <!-- :style="{visibility:exportPercent?'visible':'hidden'}"-->
                <div class="slds-grid slds-grid_align-spread slds-p-bottom_x-small" id="progress-bar-label-id-1">
                    <span>Downloaded: {{exportDownloaded}} Skipped: {{exportSkipped}} Failed: {{exportFailed}}</span>
                    <span>
                        <strong>{{exportPercent}}% Complete</strong>
                    </span>
                </div>
                <div class="slds-progress-bar">
                    <span class="slds-progress-bar__value" :style="{width:exportPercent+'%'}"></span>
                </div>
            </div>
            <ul>
                <li v-for="file in exportResultFiles">
                    {{file.name}}<br />{{file.result}}
                </li>
            </ul>
        </div>
    </div>
    <div v-if="!currentInstanceUrl" style="padding:5em;">
        <a href="javascript:void(0)" @click="dispatch('showOrgModal',true)">click here to login your organization.</a>
    </div>
</div>