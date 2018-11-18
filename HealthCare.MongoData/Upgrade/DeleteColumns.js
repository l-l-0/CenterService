db.Role.update({}, { $unset: { 'filters': '' } }, false, true);
db.User.update({}, { $unset: { 'filters': '' } }, false, true);
db["Exchange"].update({}, { $unset: { 'doctor': '', 'doctorId': '', 'patient': '', 'patientId': '' } }, false, true);
db["Exchange.Allocation"].update({}, { $unset: { 'remark': '', 'doctor': '', 'doctorId': '', 'patient': '', 'patientId': '' } }, false, true);
db.Kit.update({}, { $unset: { 'isPublic': '' } }, false, true);