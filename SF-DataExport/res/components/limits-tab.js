Vue.component('limits-tab', {
	template,
	computed: {
		currentInstanceUrl() {
			return this.$store.state.currentInstanceUrl;
		},
		orgLimits() {
			return this.$store.state.orgLimits;
		},
		userLicenses() {
			return this.$store.state.userLicenses;
		},
	},
});