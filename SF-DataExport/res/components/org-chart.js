function createOrgChart(that, id, data) {
    const node = document.getElementById(id);
    removeAllChilds(node);
    new OrgChart({
        chartContainer: '#' + id,
        createNode(node, data) {
            createOrgNode(that, node, data);
        },
        data,
        direction: 'l2r',
        nodeContent: 'name',
        nodeTitle: '_',
        pan: true,
        parentNodeSymbol: '',
        toggleSiblingsResp: true
    });
}

function createOrgNode(that, node, { name, users, url }) {
    const rLen = (users || []).length || 0;

    const titleDiv = node.querySelector('.title');
    if (titleDiv) titleDiv.innerHTML = rLen ? ['<i class="material-icons">group</i> (', rLen, ')'].join('') : '';

    const contentDiv = node.querySelector('.content');

    if (contentDiv) {
        const container = document.createDocumentFragment();
        const orgLink = document.createElement('a');
        orgLink.href = 'javascript:void(0)';
        orgLink.addEventListener('click', _ => that.dispatch('viewPage', url), true);
        orgLink.innerText = name;
        orgLink.title = name;
        container.appendChild(orgLink);

        if (rLen) {
            const userPanel = document.createElement('div');
            userPanel.className = 'users';

            for (let r = 0; r < rLen; r++) {
                const { Id, Email, Name, Profile, SmallPhotoUrl, UserRole } = users[r];
                const a = document.createElement('a');
                a.href = 'javascript:void(0)';

                if (Id) {
                    a.addEventListener('click', _ =>
                        that.dispatch('viewPage', url.replace(/^(https:\/\/[^/]+\/).*$/i, '$1') + Id + '?noredirect=1'), true);
                }

                a.innerText = Name;
                a.className = 'slds-truncate';
                a.title = Name + ' ' + Email + '\n' + (UserRole || {}).Name + '\n' + (Profile || {}).Name;
                a.style.backgroundImage = 'url(' + SmallPhotoUrl + ')';
                userPanel.appendChild(a);
            }

            container.appendChild(userPanel);
        }

        removeAllChilds(contentDiv);
        contentDiv.appendChild(container);
    }
}

function removeAllChilds(node) {
    if (node) {
        let lastContent;
        while (lastContent = node.lastChild) node.removeChild(lastContent);
    }
}

Vue.component('org-chart', {
    template,
    props: ['id', 'data'],
    mounted() {
        createOrgChart(this, this.id, this.data);
    },
    watch: {
        data(value) {
            createOrgChart(this, this.id, value);
        },
    },
});