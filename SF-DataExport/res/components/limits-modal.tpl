<v-modal @close="dispatch('showLimitsModal',false)" :modalStyle="{'min-width':'95vw',width:'95vw','max-width':'95vw'}">
    <template #header>
		<ul class="slds-tabs_default__nav">
			<li :class="['slds-tabs_default__item',tab!=='logging'?'slds-is-active':'']" title="Org Limits">
			  <a class="slds-tabs_default__link" href="javascript:void(0);" tabindex="0" @click="switchTab('index')">Org Limits</a>
			</li>
			<li :class="['slds-tabs_default__item',tab==='logging'?'slds-is-active':'']" title="Logging">
			  <a class="slds-tabs_default__link" href="javascript:void(0);" @click="switchTab('logging')">Logging</a>
			</li>
		</ul>
	</template>
    <div style="padding:1em;position:relative;">
        <div v-if="currentInstanceUrl && orgLimits.length">
			<div :style="{display:tab==='logging'?'block':'none'}">
				<canvas ref="chart" :width="chartWidth" :height="chartHeight" style="cursor:pointer;"></canvas>
				<i>
					Click on the legend to toggle visibility of line.
					<a href="javascript:void(0)" @click="showAll()">(show all)</a>
					<a href="javascript:void(0)" @click="hideAll()">(hide all)</a>
				</i>
				<div class="slds-form-element">
				  <div class="slds-form-element__control">
					<div class="slds-select_container">
					  <select class="slds-select" v-model="chartType">
						<option v-for="(name,value) in chartTypes" :value="value">Display by {{name}}</option>
					  </select>
					</div>
				  </div>
				</div>
				<cmdcopy-element label="Command line for Log Org Limits" :cmd="cmdLimits"></cmdcopy-element>
				
				<hr />
				<div class="slds-form-element">
					<button class="slds-button slds-button_success" @click="dispatch('GetLimits',{instanceUrl:currentInstanceUrl})">
						Refresh
					</button>
				</div>
			</div>
			<div :style="{display:tab==='logging'?'none':'block'}">
				<div v-for="value in orgLimits">
					<div class="slds-grid slds-grid_align-spread slds-p-bottom_x-small" id="progress-bar-label-id-1">
						<span>{{value.Name}} ( {{value.Remaining}} of {{value.Max}} left)</span>
						<span>
							<strong>{{(value.Max-value.Remaining)|percent(value.Max)|round(2)}}%</strong>
						</span>
					</div>
					<div class="slds-progress-bar slds-progress-bar_circular">
						<span class="slds-progress-bar__value" :style="{width:$options.filters.percent(value.Max-value.Remaining,value.Max)+'%',background:$options.filters.percent(value.Max-value.Remaining,value.Max)>90?'red':$options.filters.percent(value.Max-value.Remaining,value.Max)>70?'orange':''}"></span>
					</div>
					<p>&nbsp;</p>
				</div>
			</div>
		</div>
		<div style="height:6em" v-if="currentInstanceUrl && !orgLimits.length">
			<spinner class="slds-spinner slds-spinner_medium"></spinner>
		</div>
		<div v-if="!currentInstanceUrl" style="padding:5em;">
			<a href="javascript:void(0)" @click="dispatch('showOrgModal',true)">click here to login your organization.</a>
		</div>
	</div>
	<template #footer><i v-if="false"></i></template>
</v-modal>