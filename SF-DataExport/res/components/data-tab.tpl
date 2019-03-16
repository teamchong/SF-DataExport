<div>
    <div class="slds-page-header">
        <div class="slds-page-header__row">
            <div class="slds-page-header__col-title">
                <div class="slds-media">
                    <div class="slds-media__figure">
                        <span class="slds-icon_container slds-icon-standard-poll" title="Org Limits">
                            <svg class="slds-icon slds-page-header__icon">
                                <use xmlns:xlink="http://www.w3.org/1999/xlink" xlink:href="/assets/icons/standard-sprite/svg/symbols.svg#poll" />
                            </svg>
                        </span>
                    </div>
                    <div class="slds-media__body">
                        <div class="slds-page-header__name">
                            <div class="slds-page-header__name-title">
                                <h1>
                                    <span class="slds-page-header__title slds-truncate" title="Org Limits">Org Limits</span>
                                </h1>
                            </div>
                        </div><!--<p class="slds-page-header__name-meta">-</p>-->
                    </div>
                </div>
            </div>
        </div>
    </div>
    <div style="padding:1em;position:relative;">
        <div v-if="currentInstanceUrl">

            <div v-for="value in userLicenses">
                <div class="slds-grid slds-grid_align-spread slds-p-bottom_x-small" id="progress-bar-label-id-1">
                    <span>{{value.Name}} ( {{value.TotalLicenses-value.UsedLicenses}} of {{value.TotalLicenses}} left)</span>
                    <span aria-hidden="true">
                        <strong>{{percent(value.UsedLicenses,value.TotalLicenses)|round(2)}}%</strong>
                    </span>
                </div>
                <div class="slds-progress-bar slds-progress-bar_circular">
                    <span class="slds-progress-bar__value" :style="{width:percent(value.UsedLicenses,value.TotalLicenses)+'%',background:percent(value.UsedLicenses,value.TotalLicenses)>90?'red':percent(value.UsedLicenses,value.TotalLicenses)>70?'orange':''}"></span>
                </div>
                <p>&nbsp;</p>
            </div>

            <div v-for="(value, key) in orgLimits">
                <div class="slds-grid slds-grid_align-spread slds-p-bottom_x-small" id="progress-bar-label-id-1">
                    <span>{{key}} ( {{value.Remaining}} of {{value.Max}} left)</span>
					<span aria-hidden="true">
						<strong>{{percent(value.Max-value.Remaining,value.Max)|round(2)}}%</strong>
					</span>
				</div>
				<div class="slds-progress-bar slds-progress-bar_circular">
					<span class="slds-progress-bar__value" :style="{width:percent(value.Max-value.Remaining,value.Max)+'%',background:percent(value.Max-value.Remaining,value.Max)>90?'red':percent(value.Max-value.Remaining,value.Max)>70?'orange':''}"></span>
				</div>
				<p>&nbsp;</p>
			</div>

		</div>
		<spinner class="slds-spinner_medium" style="margin-top:2em;" v-if="currentInstanceUrl && !Object.keys(orgLimits).length"></spinner>
		<div v-if="!currentInstanceUrl" style="padding:5em;">
			<a href="javascript:void(0)" @click="dispatch('showOrgModal',true)">click here to login your organization.</a>
		</div>
	</div>
</div>