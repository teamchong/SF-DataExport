Vue.component('page-app', {
    template,
    computed: {
        alertMessage() { return this.$store.state.alertMessage; },
        isLoading() { return this.$store.state.isLoading; },
        showOrgModal() { return this.$store.state.showOrgModal; },
        tab() { return this.$store.state.tab; },
    },
});