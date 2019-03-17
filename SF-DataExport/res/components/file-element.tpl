<div class="slds-form-element">
    <div class="slds-clearfix">
        <label class="slds-form-element__label" :for="id">{{label}}</label>
    </div>
    <div class="slds-form-element__control" style="margin-top:0.1em;">
        <v-combobox v-model="model" :loading="loading"
			:items="items" solo dense hide-no-data no-filter :id="id" :search-input.sync="path"></v-combobox>
    </div>
</div>