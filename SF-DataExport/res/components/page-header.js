Vue.component('page-header', {
    template,
    methods: {
        globalSearchFilter({ keyPrefix, label, name }, queryText) {
            const { currentInstanceUrl } = this;
            let search = queryText ? queryText.trim() : '';

            if (currentInstanceUrl && search && search.indexOf(currentInstanceUrl + '/') === 0) {
                search = search.substr(currentInstanceUrl.length + 1).replace(/\/|.*$|\?.*$|#.*$/gi, '');
            }
            
            const searchRe = search ? new RegExp(_.escapeRegExp(search), 'i') : '';
            return !searchRe ||
                name && searchRe.test(name) ||
                label && searchRe.test(label) ||
                keyPrefix && keyPrefix.search(searchRe) === 0;
        }
    },
    computed: {
        currentInstanceUrl() { return this.$store.state.currentInstanceUrl; },
        globalSearch: {
            get() { return this.$store.state.globalSearch; },
            set(value) { this.dispatch('globalSearch', value); }
        },
        globalSearchItems() {
            const { objects, toolingObjects } = this.$store.state;
            const results = [];
            const objLen = objects ? objects.length : 0;
            for (let i = 0; i < objLen; i++) {
                const { keyPrefix, label, name } = objects[i];
                results.push({ type: 'data', keyPrefix, label, name });
                //results.push({ type: 'data', ...objects[i] });
            }
            const tObjLen = toolingObjects ? toolingObjects.length : 0;
            for (let i = 0; i < tObjLen; i++) {
                const { keyPrefix, label, name } = toolingObjects[i];
                results.push({ type: 'tooling', keyPrefix, label, name });
                //results.push({ type: 'tooling', ...toolingObjects[i] });
            }
            return results;
        },
        objectName() { return (this.globalSearch || {}).name || ''; },
        objectLabel() { return (this.globalSearch || {}).label || ''; },
        objectListPage() {
            const { currentInstanceUrl } = this.$store.state;
            const { keyPrefix } = this.globalSearch || {};
            return keyPrefix ? currentInstanceUrl + '/' + keyPrefix : '';
        },
        objectOverviewPage() {
            const { currentInstanceUrl } = this.$store.state;
            const { keyPrefix } = this.globalSearch || {};
            return keyPrefix ? currentInstanceUrl + '/' + keyPrefix + '/o' : '';
        },
        objectPrefix() { return (this.globalSearch || {}).keyPrefix || ''; },
        objectSetupPage() {
            const { currentInstanceUrl } = this.$store.state;
            const { name } = this.globalSearch || {};
            return name ? currentInstanceUrl + '/p/setup/layout/LayoutFieldList?type=' + encodeURIComponent(name) : '';
        },
        objectType() { return (this.globalSearch || {}).type || ''; },
        popoverUserId: {
            get() { return this.$store.state.popoverUserId; },
            set(value) { this.dispatch('popoverUserId', value); }
        },
        showOrgModal() { return this.$store.state.showOrgModal; },
        showUserPopover() { return this.$store.state.showUserPopover; },
        userAs() {
            const { userIdAs, users } = this.$store.state;
            for (let len = users.length, i = 0; i < len; i++) {
                const user = users[i];
                if (user.Id === userIdAs) return user;
            }
            return {};
        },
        userDisplayName() {
            return this.userAs.Name || '';
        },
        userEmail() {
            return this.userAs.Email || '';
        },
        userItems() {
            return this.$store.state.users.map(o => ({ text: o.Name + ' ' + o.Email, value: o.Id }));
        },
        userName() {
            return this.userAs.Username || '';
        },
        userPicture() {
            return this.userAs.FullPhotoUrl || '';
        },
        userProfileName() {
            return (this.userAs.Profile || {}).Name || '';
        },
        userRoleName() {
            return (this.userAs.UserRole || {}).Name || '';
        },
        userThumbnail() {
            return this.userAs.SmallPhotoUrl || '';
        }
    }
});