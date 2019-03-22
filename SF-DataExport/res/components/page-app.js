Vue.component('page-app', {
    template,
    watch: {
        popoverUserId(value) {
            if (value) {
                if (!this.$store.state.showUserPopover) {
                    this.dispatch('showUserPopover', true);
                }
            } else {
                if (this.$store.state.showUserPopover) {
                    this.dispatch('showUserPopover', false);
                }
            }
        },
        showUserPopover(value) {
            if (value) {
                if (!this.$store.state.popoverUserId) {
                    this.dispatch('showUserPopover', false);
                }
            } else {
                if (this.$store.state.popoverUserId) {
                    this.dispatch('popoverUserId', '');
                }
            }
        }
    },
    computed: {
        alertMessage() { return this.$store.state.alertMessage; },
        isLoading() { return this.$store.state.isLoading; },
        showOrgModal() { return this.$store.state.showOrgModal; },
        tab() { return this.$store.state.tab; },
        showUserPopover() { return this.$store.state.showUserPopover; },
        cmdLoginAs() {
            const { cmd, popoverUserId } = this.$store.state;
            const userParam = popoverUserId ? ' --user ' + popoverUserId : '';
            const pageParam = ' --page "/"';
            return cmd + ' loginas@' + this.currentOrgName() + userParam + pageParam;
        },
        currentInstanceUrl() { return this.$store.state.currentInstanceUrl; },
        popoverUserId() {
            return this.$store.state.popoverUserId;
        },
        popoverUser() {
            const { popoverUserId, users } = this.$store.state;
            for (let len = users.length, i = 0; i < len; i++) {
                const user = users[i];
                if (user.Id === popoverUserId) return user;
            }
            return {};
        },
        userDisplayName() {
            return this.popoverUser.Name || '';
        },
        userEmail() {
            return this.popoverUser.Email || '';
        },
        userItems() {
            return this.$store.state.users.map(o => ({ text: o.Name + ' ' + o.Email, value: o.Id }));
        },
        userName() {
            return this.popoverUser.Username || '';
        },
        userPicture() {
            return this.popoverUser.FullPhotoUrl || '';
        },
        userProfileName() {
            return (this.popoverUser.Profile || {}).Name || '';
        },
        userRoleName() {
            return (this.popoverUser.UserRole || {}).Name || '';
        }
    },
});