Vue.component('data-tab', {
	template,
	computed: {
		currentInstanceUrl() {
			return this.$store.state.currentInstanceUrl;
		}
	}
});